using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.WhiteBoxTests
{
    public class LoanWbtTests
    {
        private static async Task<int> SeedLoanAsync(LibraryDbContext db, int bookId, int memberId, DateTime? returnDate)
        {
            var loan = new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today.AddDays(-5),
                DueDate = DateTime.Today.AddDays(9),
                ReturnDate = returnDate
            };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            db.Entry(loan).State = EntityState.Detached;
            return loan.Id;
        }

        private static Loan UpdatePayload(int loanId, int bookId, int memberId, DateTime? returnDate) => new Loan
        {
            Id = loanId,
            BookId = bookId,
            MemberId = memberId,
            LoanDate = DateTime.Today.AddDays(-5),
            DueDate = DateTime.Today.AddDays(9),
            ReturnDate = returnDate
        };

        [Fact]
        public async Task Path_ActiveToReturned_SameBook_IncrementsCopies()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 1 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, book.Id, member.Id, returnDate: null);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, book.Id, member.Id, DateTime.Today));

            Assert.True(result.Succeeded);
            Assert.Equal(2, (await db.Books.FindAsync(book.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ReturnedToActive_SameBook_DecrementsCopies()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 2 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, book.Id, member.Id, returnDate: DateTime.Today);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, book.Id, member.Id, returnDate: null));

            Assert.True(result.Succeeded);
            Assert.Equal(1, (await db.Books.FindAsync(book.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ActiveToActive_SameBook_LeavesCopiesUnchanged()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 3 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, book.Id, member.Id, returnDate: null);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, book.Id, member.Id, returnDate: null));

            Assert.True(result.Succeeded);
            Assert.Equal(3, (await db.Books.FindAsync(book.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ReturnedToReturned_SameBook_LeavesCopiesUnchanged()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 4 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, book.Id, member.Id, returnDate: DateTime.Today.AddDays(-1));
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, book.Id, member.Id, returnDate: DateTime.Today));

            Assert.True(result.Succeeded);
            Assert.Equal(4, (await db.Books.FindAsync(book.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ActiveToActive_ChangeBook_AdjustsBothInventories()
        {
            using var db = InMemoryDb.Create();
            var oldBook = new Book { Title = "Old", Author = "A", ISBN = "1", AvailableCopies = 1 };
            var newBook = new Book { Title = "New", Author = "A", ISBN = "2", AvailableCopies = 1 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.AddRange(oldBook, newBook);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, oldBook.Id, member.Id, returnDate: null);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, newBook.Id, member.Id, returnDate: null));

            Assert.True(result.Succeeded);
            Assert.Equal(2, (await db.Books.FindAsync(oldBook.Id))!.AvailableCopies);
            Assert.Equal(0, (await db.Books.FindAsync(newBook.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ReturnedToReturned_ChangeBook_LeavesBothInventoriesUnchanged()
        {
            using var db = InMemoryDb.Create();
            var oldBook = new Book { Title = "Old", Author = "A", ISBN = "1", AvailableCopies = 1 };
            var newBook = new Book { Title = "New", Author = "A", ISBN = "2", AvailableCopies = 1 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.AddRange(oldBook, newBook);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, oldBook.Id, member.Id, returnDate: DateTime.Today.AddDays(-1));
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, newBook.Id, member.Id, returnDate: DateTime.Today));

            Assert.True(result.Succeeded);
            Assert.Equal(1, (await db.Books.FindAsync(oldBook.Id))!.AvailableCopies);
            Assert.Equal(1, (await db.Books.FindAsync(newBook.Id))!.AvailableCopies);
        }

        [Fact]
        public async Task Path_ReopenLoan_WithNoAvailableCopies_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 0 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, book.Id, member.Id, returnDate: DateTime.Today);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, book.Id, member.Id, returnDate: null));

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.ReturnDate) && e.ErrorMessage.Contains("no available copies"));
        }

        [Fact]
        public async Task Path_ChangeToUnavailableBook_WhileActive_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var oldBook = new Book { Title = "Old", Author = "A", ISBN = "1", AvailableCopies = 2 };
            var newBook = new Book { Title = "New", Author = "A", ISBN = "2", AvailableCopies = 0 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.AddRange(oldBook, newBook);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var loanId = await SeedLoanAsync(db, oldBook.Id, member.Id, returnDate: null);
            var service = new LoanService(db);

            var result = await service.UpdateAsync(UpdatePayload(loanId, newBook.Id, member.Id, returnDate: null));

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.BookId) && e.ErrorMessage.Contains("unavailable"));
        }
    }
}
