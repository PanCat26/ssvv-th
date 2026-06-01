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
    public class LoanWorkflowIntegrationTests
    {
        private static async Task<(int bookId, int memberId)> SeedAsync(Data.LibraryDbContext db, int availableCopies)
        {
            var book = new Book { Title = "Working Effectively with Legacy Code", Author = "Michael Feathers", ISBN = "9780131177055", AvailableCopies = availableCopies };
            var member = new Member { Name = "Barbara Liskov", Email = "barbara@mit.edu" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return (book.Id, member.Id);
        }

        [Fact]
        public async Task FullLifecycle_BorrowReturnDelete_KeepsInventoryConsistent()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var created = await service.CreateAsync(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) });
            Assert.True(created.Succeeded);
            Assert.Equal(1, (await db.Books.FindAsync(bookId))!.AvailableCopies);

            db.Entry(created.Loan!).State = EntityState.Detached;
            var returned = await service.UpdateAsync(new Loan
            {
                Id = created.Loan!.Id,
                BookId = bookId,
                MemberId = memberId,
                LoanDate = DateTime.Today,
                DueDate = DateTime.Today.AddDays(14),
                ReturnDate = DateTime.Today.AddDays(3)
            });
            Assert.True(returned.Succeeded);
            Assert.Equal(2, (await db.Books.FindAsync(bookId))!.AvailableCopies);

            var deleted = await service.DeleteAsync(created.Loan!.Id);
            Assert.True(deleted);
            Assert.Equal(2, (await db.Books.FindAsync(bookId))!.AvailableCopies);
            Assert.Null(await service.GetByIdAsync(created.Loan!.Id));
        }

        [Fact]
        public async Task DeletingActiveLoan_RestoresInventory()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 1);
            var service = new LoanService(db);

            var created = await service.CreateAsync(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) });
            Assert.True(created.Succeeded);
            Assert.Equal(0, (await db.Books.FindAsync(bookId))!.AvailableCopies);

            var deleted = await service.DeleteAsync(created.Loan!.Id);

            Assert.True(deleted);
            Assert.Equal(1, (await db.Books.FindAsync(bookId))!.AvailableCopies);
        }

        [Fact]
        public async Task CreatedLoan_ExposesBookAndMemberNavigation()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 2);
            var service = new LoanService(db);

            var created = await service.CreateAsync(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) });
            var loaded = await service.GetByIdAsync(created.Loan!.Id);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.Book);
            Assert.NotNull(loaded.Member);
            Assert.Equal("Working Effectively with Legacy Code", loaded.Book!.Title);
            Assert.Equal("Barbara Liskov", loaded.Member!.Name);
        }

        [Fact]
        public async Task LoanController_Create_DecrementsInventoryAndRedirects()
        {
            using var db = InMemoryDb.Create();
            var (bookId, memberId) = await SeedAsync(db, availableCopies: 3);
            var controller = new LoanController(new LoanService(db), new BookService(db), new MemberService(db));
            ControllerTestHelper.AttachTempData(controller);

            var result = await controller.Create(new Loan { BookId = bookId, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(14) });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(LoanController.Index), redirect.ActionName);
            Assert.Equal(2, (await db.Books.FindAsync(bookId))!.AvailableCopies);
            Assert.True(await db.Loans.AnyAsync(l => l.BookId == bookId && l.MemberId == memberId));
        }
    }
}
