using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ssvv_th.Controllers;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.BackendTests
{
    public class FakeTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    public class LoanControllerTests
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

        private void SetTempData(Controller controller)
        {
            controller.TempData = new TempDataDictionary(
                new DefaultHttpContext(),
                new FakeTempDataProvider());
        }

        [Fact]
        public async Task LoanController_Create_WithDueDateBeforeLoanDate_ReturnsViewWithModelError()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var book = new Book { Title = "TDD By Example", AvailableCopies = 2 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loanService = new LoanService(db);
            var bookService = new BookService(db);
            var memberService = new MemberService(db);
            var controller = new LoanController(loanService, bookService, memberService);
            SetTempData(controller);

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today.AddDays(5),
                DueDate = DateTime.Today // Due date is before loan date
            };

            // Act
            var result = await controller.Create(loan);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("DueDate"));
            Assert.Equal(loan, viewResult.Model);
        }

        [Fact]
        public async Task LoanController_Create_WithReturnDateBeforeLoanDate_ReturnsViewWithModelError()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var book = new Book { Title = "TDD By Example", AvailableCopies = 2 };
            var member = new Member { Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            var loanService = new LoanService(db);
            var bookService = new BookService(db);
            var memberService = new MemberService(db);
            var controller = new LoanController(loanService, bookService, memberService);
            SetTempData(controller);

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today.AddDays(5),
                ReturnDate = DateTime.Today // Return date is before loan date
            };

            // Act
            var result = await controller.Create(loan);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("ReturnDate"));
            Assert.Equal(loan, viewResult.Model);
        }

        [Fact]
        public async Task LoanController_Edit_WithMismatchedId_ReturnsBadRequest()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            SetTempData(controller);
            var loan = new Loan { Id = 1, BookId = 1, MemberId = 1 };

            // Act
            var result = await controller.Edit(2, loan); // Mismatched ID: 2 vs 1

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task LoanController_Edit_WithValidData_UpdatesAndRedirects()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var book = new Book { Id = 10, Title = "TDD By Example", AvailableCopies = 2 };
            var member = new Member { Id = 20, Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);

            var loan = new Loan { Id = 1, BookId = 10, MemberId = 20, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            SetTempData(controller);

            // Modify Loan
            loan.ReturnDate = DateTime.Today.AddDays(2);

            // Act
            var result = await controller.Edit(1, loan);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(LoanController.Index), redirectResult.ActionName);
            Assert.True(controller.ModelState.IsValid);

            // Verify DB got updated
            var updated = await db.Loans.FindAsync(1);
            Assert.NotNull(updated);
            Assert.Equal(DateTime.Today.AddDays(2), updated.ReturnDate);
        }

        [Fact]
        public async Task LoanController_Edit_WithInvalidDates_ReturnsViewWithModelError()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var book = new Book { Id = 10, Title = "TDD By Example", AvailableCopies = 2 };
            var member = new Member { Id = 20, Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);

            var loan = new Loan { Id = 1, BookId = 10, MemberId = 20, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            SetTempData(controller);

            // Set invalid reverse dates
            loan.DueDate = DateTime.Today.AddDays(-5); // Due date is before loan date

            // Act
            var result = await controller.Edit(1, loan);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("DueDate"));
        }

        [Fact]
        public async Task LoanController_Delete_NonExistentId_ReturnsNotFound()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            SetTempData(controller);

            // Act
            var result = await controller.Delete(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task LoanController_DeleteConfirmed_WithValidId_DeletesAndRedirects()
        {
            // Arrange
            using var db = CreateInMemoryDbContext();
            var book = new Book { Id = 10, Title = "TDD By Example", AvailableCopies = 2 };
            var member = new Member { Id = 20, Name = "Tester" };
            db.Books.Add(book);
            db.Members.Add(member);

            var loan = new Loan { Id = 1, BookId = 10, MemberId = 20, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) };
            db.Loans.Add(loan);
            await db.SaveChangesAsync();

            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            SetTempData(controller);

            // Act
            var result = await controller.DeleteConfirmed(1);

            // Assert
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(LoanController.Index), redirectResult.ActionName);

            // Verify deleted from DB
            var deleted = await db.Loans.FindAsync(1);
            Assert.Null(deleted);
        }
    }
}
