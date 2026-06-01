using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.Services
{
    public class LoanServiceBbtTests
    {
        private LibraryDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new LibraryDbContext(options);
        }

        private async Task<(LibraryDbContext Context, Book Book, Member Member)> SetupDatabaseAsync(string dbName, int availableCopies = 5)
        {
            var context = GetDbContext(dbName);
            var book = new Book { Title = "Test Book", Author = "Author", ISBN = "123", AvailableCopies = availableCopies };
            var member = new Member { Name = "John Doe", Email = "john@example.com", Phone = "1234" };
            
            context.Books.Add(book);
            context.Members.Add(member);
            await context.SaveChangesAsync();

            return (context, book, member);
        }

        [Fact]
        public async Task CreateAsync_ValidLoan_ReturnsSuccess()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book.Id, MemberId = setup.Member.Id, LoanDate = new DateTime(2023, 10, 1), DueDate = new DateTime(2023, 10, 15) };
            var result = await service.CreateAsync(loan);

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task CreateAsync_DueDateBeforeLoanDate_ReturnsError()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book.Id, MemberId = setup.Member.Id, LoanDate = new DateTime(2023, 10, 15), DueDate = new DateTime(2023, 10, 14) };
            var result = await service.CreateAsync(loan);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == "DueDate");
        }

        [Fact]
        public async Task CreateAsync_ReturnDateBeforeLoanDate_ReturnsError()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book.Id, MemberId = setup.Member.Id, LoanDate = new DateTime(2023, 10, 15), DueDate = new DateTime(2023, 10, 20), ReturnDate = new DateTime(2023, 10, 14) };
            var result = await service.CreateAsync(loan);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == "ReturnDate");
        }

        [Fact]
        public async Task CreateAsync_NonExistentBook_ReturnsError()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = 999, MemberId = setup.Member.Id, LoanDate = new DateTime(2023, 10, 1), DueDate = new DateTime(2023, 10, 15) };
            var result = await service.CreateAsync(loan);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == "BookId" && e.ErrorMessage.Contains("does not exist"));
        }

        [Fact]
        public async Task CreateAsync_NonExistentMember_ReturnsError()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString());
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book.Id, MemberId = 999, LoanDate = new DateTime(2023, 10, 1), DueDate = new DateTime(2023, 10, 15) };
            var result = await service.CreateAsync(loan);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == "MemberId" && e.ErrorMessage.Contains("does not exist"));
        }

        [Fact]
        public async Task CreateAsync_BookUnavailable_ReturnsError()
        {
            var setup = await SetupDatabaseAsync(Guid.NewGuid().ToString(), availableCopies: 0);
            var service = new LoanService(setup.Context);

            var loan = new Loan { BookId = setup.Book.Id, MemberId = setup.Member.Id, LoanDate = new DateTime(2023, 10, 1), DueDate = new DateTime(2023, 10, 15) };
            var result = await service.CreateAsync(loan);

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == "BookId" && e.ErrorMessage.Contains("currently unavailable"));
        }
    }
}
