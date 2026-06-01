using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.BlackBoxTests
{
    public class LoanBbtTests
    {
        private static async Task<(int bookId, int memberId)> SeedAsync(Data.LibraryDbContext db, int availableCopies)
        {
            var book = new Book { Title = "Domain-Driven Design", Author = "Eric Evans", ISBN = "9780321125217", AvailableCopies = availableCopies };
            var member = new Member { Name = "Alice Smith", Email = "alice@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return (book.Id, member.Id);
        }

        [Fact]
        public async Task CreateAsync_ValidLoan_ReturnsSuccess()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            });

            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task CreateAsync_DueDateEqualsLoanDate_ReturnsSuccess()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today
            });

            Assert.True(result.Succeeded);
        }

        [Fact]
        public async Task CreateAsync_DueDateOneDayBeforeLoanDate_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(-1)
            });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.DueDate));
        }

        [Fact]
        public async Task CreateAsync_ReturnDateBeforeLoanDate_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14),
                ReturnDate = DateTime.Today.AddDays(-1)
            });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.ReturnDate));
        }

        [Fact]
        public async Task CreateAsync_NonExistentBook_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var (_, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = 9999,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.BookId));
        }

        [Fact]
        public async Task CreateAsync_NonExistentMember_ReturnsError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, _) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = 9999,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.MemberId));
        }

        [Fact]
        public async Task CreateAsync_AvailableCopiesZero_ReturnsUnavailableError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 0);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            });

            Assert.False(result.Succeeded);
            Assert.Contains(result.Errors, e => e.PropertyName == nameof(Loan.BookId) && e.ErrorMessage.Contains("unavailable"));
        }

        [Fact]
        public async Task CreateAsync_LastAvailableCopy_ReturnsSuccessAndReachesZero()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 1);
            var service = new LoanService(db);

            var result = await service.CreateAsync(new Loan
            {
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14)
            });

            Assert.True(result.Succeeded);
            var book = await db.Books.FindAsync(bookId);
            Assert.Equal(0, book!.AvailableCopies);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public void LoanModel_BookIdRange_IsValidated(int bookId, bool expectedValid)
        {
            var loan = new Loan { BookId = bookId, MemberId = 1, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) };
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(loan, new ValidationContext(loan), results, validateAllProperties: true);

            Assert.Equal(expectedValid, !results.Any(r => r.MemberNames.Contains(nameof(Loan.BookId))));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        public void LoanModel_MemberIdRange_IsValidated(int memberId, bool expectedValid)
        {
            var loan = new Loan { BookId = 1, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) };
            var results = new List<ValidationResult>();
            Validator.TryValidateObject(loan, new ValidationContext(loan), results, validateAllProperties: true);

            Assert.Equal(expectedValid, !results.Any(r => r.MemberNames.Contains(nameof(Loan.MemberId))));
        }

        [Fact]
        public void LoanModel_ValidData_PassesValidation()
        {
            var loan = new Loan { BookId = 1, MemberId = 1, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            var results = new List<ValidationResult>();
            var isValid = Validator.TryValidateObject(loan, new ValidationContext(loan), results, validateAllProperties: true);

            Assert.True(isValid);
            Assert.Empty(results);
        }

        [Theory]
        [InlineData(0, "Returned")]
        [InlineData(-1, "Overdue")]
        [InlineData(1, "Active")]
        public void LoanModel_StatusCalculation_ReturnsExpectedStatus(int dueDateOffset, string expectedStatus)
        {
            var loan = new Loan
            {
                BookId = 1,
                MemberId = 1,
                LoanDate = DateTime.Today.AddDays(-10),
                DueDate = DateTime.Today.AddDays(dueDateOffset)
            };
            if (expectedStatus == "Returned")
                loan.ReturnDate = DateTime.Today;

            Assert.Equal(expectedStatus, loan.Status);
        }
    }
}
