using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Controllers;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.IntegrationTests
{
    public class LoanControllerIntegrationTests
    {
        private static LoanController NewController(Data.LibraryDbContext db)
        {
            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            ControllerTestHelper.AttachTempData(controller);
            return controller;
        }

        private static async Task<(int bookId, int memberId)> SeedBookAndMemberAsync(Data.LibraryDbContext db)
        {
            var book = new Book { Title = "B", Author = "A", ISBN = "1", AvailableCopies = 2 };
            var member = new Member { Name = "M", Email = "m@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return (book.Id, member.Id);
        }

        [Fact]
        public async Task Create_WithDueDateBeforeLoanDate_ReturnsViewWithModelError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedBookAndMemberAsync(db);
            var controller = NewController(db);

            var result = await controller.Create(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today.AddDays(5), DueDate = DateTime.Today });

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(Loan.DueDate)));
        }

        [Fact]
        public async Task Create_WithReturnDateBeforeLoanDate_ReturnsViewWithModelError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedBookAndMemberAsync(db);
            var controller = NewController(db);

            var result = await controller.Create(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today.AddDays(5), DueDate = DateTime.Today.AddDays(10), ReturnDate = DateTime.Today });

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(Loan.ReturnDate)));
        }

        [Fact]
        public async Task Edit_WithMismatchedId_ReturnsBadRequest()
        {
            using var db = InMemoryDb.Create();
            var controller = NewController(db);

            var result = await controller.Edit(2, new Loan { Id = 1, BookId = 1, MemberId = 1 });

            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task Edit_WithValidData_UpdatesAndRedirects()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedBookAndMemberAsync(db);
            var loan = new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            db.Entry(loan).State = EntityState.Detached;
            var controller = NewController(db);

            var result = await controller.Edit(loan.Id, new Loan { Id = loan.Id, BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14), ReturnDate = DateTime.Today.AddDays(2) });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(LoanController.Index), redirect.ActionName);
            Assert.Equal(DateTime.Today.AddDays(2), (await db.Loans.FindAsync(loan.Id))!.ReturnDate);
        }

        [Fact]
        public async Task Edit_WithInvalidDates_ReturnsViewWithModelError()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedBookAndMemberAsync(db);
            var loan = new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            db.Entry(loan).State = EntityState.Detached;
            var controller = NewController(db);

            var result = await controller.Edit(loan.Id, new Loan { Id = loan.Id, BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(-5) });

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(Loan.DueDate)));
        }

        [Fact]
        public async Task Delete_NonExistentId_ReturnsNotFound()
        {
            using var db = InMemoryDb.Create();
            var controller = NewController(db);

            var result = await controller.Delete(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteConfirmed_WithValidId_DeletesAndRedirects()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedBookAndMemberAsync(db);
            var loan = new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            var controller = NewController(db);

            var result = await controller.DeleteConfirmed(loan.Id);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(LoanController.Index), redirect.ActionName);
            Assert.Null(await db.Loans.FindAsync(loan.Id));
        }
    }
}
