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
    public class CrudIntegrationTests
    {
        private static BookController NewBookController(Data.LibraryDbContext db)
        {
            var controller = new BookController(new BookService(db));
            ControllerTestHelper.AttachTempData(controller);
            return controller;
        }

        private static MemberController NewMemberController(Data.LibraryDbContext db)
        {
            var controller = new MemberController(new MemberService(db));
            ControllerTestHelper.AttachTempData(controller);
            return controller;
        }

        [Fact]
        public async Task Book_Create_PersistsThroughAllLayers()
        {
            using var db = InMemoryDb.Create();
            var controller = NewBookController(db);

            var result = await controller.Create(new Book { Title = "Patterns of EAA", Author = "Martin Fowler", ISBN = "9780321127426", AvailableCopies = 4 });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(BookController.Index), redirect.ActionName);
            Assert.True(await db.Books.AnyAsync(b => b.Title == "Patterns of EAA"));
        }

        [Fact]
        public async Task Book_Edit_UpdatesPersistedRow()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Old Title", Author = "Author", ISBN = "123", AvailableCopies = 1 };
            db.Books.Add(book);
            await db.SaveChangesAsync();
            db.Entry(book).State = EntityState.Detached;
            var controller = NewBookController(db);

            var result = await controller.Edit(book.Id, new Book { Id = book.Id, Title = "New Title", Author = "Author", ISBN = "123", AvailableCopies = 7 });

            Assert.IsType<RedirectToActionResult>(result);
            var stored = await db.Books.FindAsync(book.Id);
            Assert.Equal("New Title", stored!.Title);
            Assert.Equal(7, stored.AvailableCopies);
        }

        [Fact]
        public async Task Book_Delete_RemovesPersistedRow()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Disposable", Author = "Author", ISBN = "123", AvailableCopies = 1 };
            db.Books.Add(book);
            await db.SaveChangesAsync();
            var controller = NewBookController(db);

            var result = await controller.DeleteConfirmed(book.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.False(await db.Books.AnyAsync(b => b.Id == book.Id));
        }

        [Fact]
        public async Task Book_Delete_WhenReferencedByLoan_IsBlockedWithError()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Referenced", Author = "Author", ISBN = "123", AvailableCopies = 2 };
            var member = new Member { Name = "Holder", Email = "holder@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.Loans.Add(new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
            await db.SaveChangesAsync();
            var controller = NewBookController(db);

            var result = await controller.DeleteConfirmed(book.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["Error"]);
            Assert.True(await db.Books.AnyAsync(b => b.Id == book.Id));
        }

        [Fact]
        public async Task Member_Create_PersistsThroughAllLayers()
        {
            using var db = InMemoryDb.Create();
            var controller = NewMemberController(db);

            var result = await controller.Create(new Member { Name = "Ada Lovelace", Email = "ada@analytical.engine" });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(MemberController.Index), redirect.ActionName);
            Assert.True(await db.Members.AnyAsync(m => m.Name == "Ada Lovelace"));
        }

        [Fact]
        public async Task Member_Edit_UpdatesPersistedRow()
        {
            using var db = InMemoryDb.Create();
            var member = new Member { Name = "Old Name", Email = "old@example.com", Phone = "111" };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.Entry(member).State = EntityState.Detached;
            var controller = NewMemberController(db);

            var result = await controller.Edit(member.Id, new Member { Id = member.Id, Name = "New Name", Email = "new@example.com", Phone = "222" });

            Assert.IsType<RedirectToActionResult>(result);
            var stored = await db.Members.FindAsync(member.Id);
            Assert.Equal("New Name", stored!.Name);
            Assert.Equal("new@example.com", stored.Email);
        }

        [Fact]
        public async Task Member_Delete_RemovesPersistedRow()
        {
            using var db = InMemoryDb.Create();
            var member = new Member { Name = "Disposable", Email = "disposable@example.com" };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var controller = NewMemberController(db);

            var result = await controller.DeleteConfirmed(member.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.False(await db.Members.AnyAsync(m => m.Id == member.Id));
        }

        [Fact]
        public async Task Member_Delete_WhenReferencedByLoan_IsBlockedWithError()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Book", Author = "Author", ISBN = "123", AvailableCopies = 2 };
            var member = new Member { Name = "Referenced", Email = "referenced@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.Loans.Add(new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
            await db.SaveChangesAsync();
            var controller = NewMemberController(db);

            var result = await controller.DeleteConfirmed(member.Id);

            Assert.IsType<RedirectToActionResult>(result);
            Assert.NotNull(controller.TempData["Error"]);
            Assert.True(await db.Members.AnyAsync(m => m.Id == member.Id));
        }
    }
}
