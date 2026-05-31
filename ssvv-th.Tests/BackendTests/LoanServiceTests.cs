using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.BackendTests
{
    public class LoanServiceTests
    {
        private LibraryDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var context = new LibraryDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task LoanService_CreateAndRetrieve_SavesToDatabase()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "TDD By Example", Author = "Kent Beck", ISBN = "123456", AvailableCopies = 2 };
            var member = new Member { Name = "Tester", Email = "test@test.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            };

            // Act
            var result = await service.CreateAsync(loan);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Loan);

            var retrieved = await service.GetByIdAsync(result.Loan.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(book.Id, retrieved.BookId);
            Assert.Equal(member.Id, retrieved.MemberId);
            Assert.Null(retrieved.ReturnDate);
        }

        [Fact]
        public async Task LoanService_UpdateLoan_UpdatesSuccessfully()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Id = 10, Title = "TDD By Example", Author = "Kent Beck", ISBN = "123456", AvailableCopies = 2 };
            var member = new Member { Id = 20, Name = "Tester", Email = "test@test.com" };
            db.Books.Add(book);
            db.Members.Add(member);

            var loan = new Loan { BookId = 10, MemberId = 20, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            // Detach to avoid local tracking conflict
            db.Entry(loan).State = EntityState.Detached;

            var updatePayload = new Loan
            {
                Id = loan.Id,
                BookId = 10,
                MemberId = 20,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14),
                ReturnDate = DateTime.Today.AddDays(5)
            };

            // Act
            var result = await service.UpdateAsync(updatePayload);

            // Assert
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Loan);
            Assert.Equal(DateTime.Today.AddDays(5), result.Loan.ReturnDate);
        }

        [Fact]
        public async Task LoanService_DeleteLoan_RemovesFromDatabase()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Id = 10, Title = "TDD By Example", Author = "Kent Beck", ISBN = "123456", AvailableCopies = 2 };
            var member = new Member { Id = 20, Name = "Tester", Email = "test@test.com" };
            db.Books.Add(book);
            db.Members.Add(member);

            var loan = new Loan { BookId = 10, MemberId = 20, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            // Act
            var deleted = await service.DeleteAsync(loan.Id);
            var retrieved = await service.GetByIdAsync(loan.Id);

            // Assert
            Assert.True(deleted);
            Assert.Null(retrieved);
        }

        #region Advanced Business Rules & Inventory Tests

        [Fact]
        public async Task LoanService_Create_WithUnavailableBook_ReturnsFailure()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "Unavailable Book", AvailableCopies = 0 }; // 0 Copies
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            };

            // Act
            var result = await service.CreateAsync(loan);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.BookId) && e.ErrorMessage.Contains("unavailable"));
        }

        [Fact]
        public async Task LoanService_Create_WithNonExistentBookId_ReturnsFailure()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var member = new Member { Name = "Tester" };
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loan = new Loan
            {
                BookId = 9999, // Fake
                MemberId = member.Id,
                LoanDate = DateTime.Today
            };

            // Act
            var result = await service.CreateAsync(loan);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.BookId) && e.ErrorMessage.Contains("does not exist"));
        }

        [Fact]
        public async Task LoanService_Create_WithNonExistentMemberId_ReturnsFailure()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "Book", AvailableCopies = 2 };
            db.Books.Add(book);
            await db.SaveChangesAsync();

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = 9999, // Fake
                LoanDate = DateTime.Today
            };

            // Act
            var result = await service.CreateAsync(loan);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.MemberId) && e.ErrorMessage.Contains("does not exist"));
        }

        [Fact]
        public async Task LoanService_Create_DecrementsBookAvailableCopies()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "Book", AvailableCopies = 5 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loan = new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today };

            // Act
            var result = await service.CreateAsync(loan);

            // Assert
            Assert.True(result.Succeeded);
            var updatedBook = await db.Books.FindAsync(book.Id);
            Assert.NotNull(updatedBook);
            Assert.Equal(4, updatedBook.AvailableCopies); // 5 -> 4
        }

        [Fact]
        public async Task LoanService_Update_IncrementsBookAvailableCopiesOnReturn()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "Book", AvailableCopies = 3 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            // Create active loan (decreases copies to 2)
            var loan = new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            var createResult = await service.CreateAsync(loan);
            Assert.True(createResult.Succeeded);

            var bookAfterCreate = await db.Books.FindAsync(book.Id);
            Assert.Equal(2, bookAfterCreate!.AvailableCopies);

            // Detach to avoid local tracking conflict
            db.Entry(createResult.Loan!).State = EntityState.Detached;

            var updatePayload = new Loan
            {
                Id = createResult.Loan!.Id,
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14),
                ReturnDate = DateTime.Today // Set returned
            };

            // Act
            var updateResult = await service.UpdateAsync(updatePayload);

            // Assert
            Assert.True(updateResult.Succeeded);
            var bookAfterReturn = await db.Books.FindAsync(book.Id);
            Assert.Equal(3, bookAfterReturn!.AvailableCopies); // 2 -> 3
        }

        [Fact]
        public async Task LoanService_Delete_IncrementsBookAvailableCopiesForActiveLoan()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            var book = new Book { Title = "Book", AvailableCopies = 4 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            // Create active loan (copies to 3)
            var loan = new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today };
            var createResult = await service.CreateAsync(loan);
            Assert.True(createResult.Succeeded);

            // Act
            var deleteResult = await service.DeleteAsync(createResult.Loan!.Id);

            // Assert
            Assert.True(deleteResult);
            var updatedBook = await db.Books.FindAsync(book.Id);
            Assert.Equal(4, updatedBook!.AvailableCopies); // 3 -> 4
        }

        [Fact]
        public async Task LoanService_ReopenLoan_WithUnavailableBook_ReturnsFailure()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var service = new LoanService(db);

            // Seed out of stock book, but let's seed it as 0 copies.
            var book = new Book { Title = "Book", AvailableCopies = 0 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            // Create a returned loan directly via DB context
            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today.AddDays(-10),
                DueDate = DateTime.Today,
                ReturnDate = DateTime.Today.AddDays(-2) // Returned
            };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            // Detach to avoid local tracking conflict
            db.Entry(loan).State = EntityState.Detached;

            var updatePayload = new Loan
            {
                Id = loan.Id,
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today.AddDays(-10),
                DueDate = DateTime.Today,
                ReturnDate = null // Reopening
            };

            // Act
            var result = await service.UpdateAsync(updatePayload);

            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.ReturnDate) && e.ErrorMessage.Contains("no available copies"));
        }

        #endregion
    }
}
