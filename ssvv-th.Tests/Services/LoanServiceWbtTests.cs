using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.Services
{
    public class LoanServiceWbtTests
    {
        private LibraryDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new LibraryDbContext(options);
        }

        private async Task<(LibraryDbContext Context, Book Book1, Book Book2, Member Member)> SetupDatabaseAsync(string dbName)
        {
            var context = GetDbContext(dbName);
            var book1 = new Book { Title = "Book 1", Author = "A1", ISBN = "111", AvailableCopies = 5 };
            var book2 = new Book { Title = "Book 2", Author = "A2", ISBN = "222", AvailableCopies = 3 };
            var member = new Member { Name = "Alice Smith", Email = "alice@test.com", Phone = "111" };

            context.Books.AddRange(book1, book2);
            context.Members.Add(member);
            await context.SaveChangesAsync();

            return (context, book1, book2, member);
        }

        private Loan CloneLoan(Loan loan)
        {
            return new Loan
            {
                Id = loan.Id,
                BookId = loan.BookId,
                MemberId = loan.MemberId,
                LoanDate = loan.LoanDate,
                DueDate = loan.DueDate,
                ReturnDate = loan.ReturnDate
            };
        }

        [Fact]
        public async Task UpdateAsync_ActiveToActive_SameBook_NoInventoryChange()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book1.Id, MemberId = setup.Member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            await service.CreateAsync(loan);
            var availableBefore = setup.Book1.AvailableCopies; 

            var updatedLoan = CloneLoan(loan);
            updatedLoan.DueDate = DateTime.Today.AddDays(20); 
            var result = await service.UpdateAsync(updatedLoan);

            Assert.True(result.Succeeded);
            var bookFromDb = await setup.Context.Books.FindAsync(setup.Book1.Id);
            Assert.Equal(availableBefore, bookFromDb!.AvailableCopies);
        }

        [Fact]
        public async Task UpdateAsync_ActiveToReturned_SameBook_IncrementsInventory()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book1.Id, MemberId = setup.Member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            await service.CreateAsync(loan);
            var availableBefore = setup.Book1.AvailableCopies; 

            var updatedLoan = CloneLoan(loan);
            updatedLoan.ReturnDate = DateTime.Today; 
            var result = await service.UpdateAsync(updatedLoan);

            Assert.True(result.Succeeded);
            var bookFromDb = await setup.Context.Books.FindAsync(setup.Book1.Id);
            Assert.Equal(availableBefore + 1, bookFromDb!.AvailableCopies);
        }

        [Fact]
        public async Task UpdateAsync_ReturnedToReturned_SameBook_NoInventoryChange()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book1.Id, MemberId = setup.Member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            await service.CreateAsync(loan);
            
            var updatedLoan1 = CloneLoan(loan);
            updatedLoan1.ReturnDate = DateTime.Today;
            await service.UpdateAsync(updatedLoan1);
            
            var availableBefore = (await setup.Context.Books.FindAsync(setup.Book1.Id))!.AvailableCopies; 

            var updatedLoan2 = CloneLoan(updatedLoan1);
            updatedLoan2.DueDate = DateTime.Today.AddDays(20); 
            var result = await service.UpdateAsync(updatedLoan2);

            Assert.True(result.Succeeded);
            var bookFromDb = await setup.Context.Books.FindAsync(setup.Book1.Id);
            Assert.Equal(availableBefore, bookFromDb!.AvailableCopies);
        }

        [Fact]
        public async Task UpdateAsync_ReturnedToActive_SameBook_DecrementsInventory()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book1.Id, MemberId = setup.Member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            await service.CreateAsync(loan);
            
            var updatedLoan1 = CloneLoan(loan);
            updatedLoan1.ReturnDate = DateTime.Today;
            await service.UpdateAsync(updatedLoan1);
            
            var availableBefore = (await setup.Context.Books.FindAsync(setup.Book1.Id))!.AvailableCopies; 

            var updatedLoan2 = CloneLoan(updatedLoan1);
            updatedLoan2.ReturnDate = null; 
            var result = await service.UpdateAsync(updatedLoan2);

            Assert.True(result.Succeeded);
            var bookFromDb = await setup.Context.Books.FindAsync(setup.Book1.Id);
            Assert.Equal(availableBefore - 1, bookFromDb!.AvailableCopies);
        }

        [Fact]
        public async Task UpdateAsync_ActiveToActive_ChangeBook_AdjustsInventories()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book1.Id, MemberId = setup.Member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            await service.CreateAsync(loan);
            var book1AvailableBefore = setup.Book1.AvailableCopies; 
            var book2AvailableBefore = setup.Book2.AvailableCopies; 

            var updatedLoan = CloneLoan(loan);
            updatedLoan.BookId = setup.Book2.Id; 
            var result = await service.UpdateAsync(updatedLoan);

            Assert.True(result.Succeeded);
            var book1FromDb = await setup.Context.Books.FindAsync(setup.Book1.Id);
            var book2FromDb = await setup.Context.Books.FindAsync(setup.Book2.Id);

            Assert.Equal(book1AvailableBefore + 1, book1FromDb!.AvailableCopies);
            Assert.Equal(book2AvailableBefore - 1, book2FromDb!.AvailableCopies);
        }
    }
}
