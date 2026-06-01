using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.WhiteBoxTests
{
    public class BookMemberWbtTests
    {
        [Fact]
        public async Task BookUpdate_NonExistentId_ReturnsNull()
        {
            using var db = InMemoryDb.Create();
            var service = new BookService(db);

            var result = await service.UpdateAsync(new Book { Id = 404, Title = "Ghost", Author = "None", ISBN = "0" });

            Assert.Null(result);
        }

        [Fact]
        public async Task BookUpdate_ExistingId_PersistsEveryField()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Old", Author = "Old Author", ISBN = "111", AvailableCopies = 1 };
            db.Books.Add(book);
            await db.SaveChangesAsync();
            var service = new BookService(db);

            var result = await service.UpdateAsync(new Book { Id = book.Id, Title = "New", Author = "New Author", ISBN = "222", AvailableCopies = 9 });

            Assert.NotNull(result);
            Assert.Equal("New", result!.Title);
            Assert.Equal("New Author", result.Author);
            Assert.Equal("222", result.ISBN);
            Assert.Equal(9, result.AvailableCopies);
        }

        [Fact]
        public async Task BookDelete_NonExistentId_ReturnsFalse()
        {
            using var db = InMemoryDb.Create();
            var service = new BookService(db);

            var deleted = await service.DeleteAsync(404);

            Assert.False(deleted);
        }

        [Fact]
        public async Task BookDelete_WithoutRelatedLoans_RemovesAndReturnsTrue()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Removable", Author = "Author", ISBN = "333", AvailableCopies = 2 };
            db.Books.Add(book);
            await db.SaveChangesAsync();
            var service = new BookService(db);

            var deleted = await service.DeleteAsync(book.Id);

            Assert.True(deleted);
            Assert.False(await db.Books.AnyAsync(b => b.Id == book.Id));
        }

        [Fact]
        public async Task BookDelete_WithRelatedLoans_ThrowsInvalidOperation()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Referenced", Author = "Author", ISBN = "444", AvailableCopies = 2 };
            var member = new Member { Name = "Holder", Email = "holder@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.Loans.Add(new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
            await db.SaveChangesAsync();
            var service = new BookService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(book.Id));
        }

        [Fact]
        public async Task MemberUpdate_NonExistentId_ReturnsNull()
        {
            using var db = InMemoryDb.Create();
            var service = new MemberService(db);

            var result = await service.UpdateAsync(new Member { Id = 404, Name = "Ghost", Email = "ghost@example.com" });

            Assert.Null(result);
        }

        [Fact]
        public async Task MemberUpdate_ExistingId_PersistsEveryField()
        {
            using var db = InMemoryDb.Create();
            var member = new Member { Name = "Old", Email = "old@example.com", Phone = "111" };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var service = new MemberService(db);

            var result = await service.UpdateAsync(new Member { Id = member.Id, Name = "New", Email = "new@example.com", Phone = "222" });

            Assert.NotNull(result);
            Assert.Equal("New", result!.Name);
            Assert.Equal("new@example.com", result.Email);
            Assert.Equal("222", result.Phone);
        }

        [Fact]
        public async Task MemberDelete_NonExistentId_ReturnsFalse()
        {
            using var db = InMemoryDb.Create();
            var service = new MemberService(db);

            var deleted = await service.DeleteAsync(404);

            Assert.False(deleted);
        }

        [Fact]
        public async Task MemberDelete_WithoutRelatedLoans_RemovesAndReturnsTrue()
        {
            using var db = InMemoryDb.Create();
            var member = new Member { Name = "Removable", Email = "removable@example.com" };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            var service = new MemberService(db);

            var deleted = await service.DeleteAsync(member.Id);

            Assert.True(deleted);
            Assert.False(await db.Members.AnyAsync(m => m.Id == member.Id));
        }

        [Fact]
        public async Task MemberDelete_WithRelatedLoans_ThrowsInvalidOperation()
        {
            using var db = InMemoryDb.Create();
            var book = new Book { Title = "Book", Author = "Author", ISBN = "555", AvailableCopies = 2 };
            var member = new Member { Name = "Referenced", Email = "referenced@example.com" };
            db.Books.Add(book);
            db.Members.Add(member);
            await db.SaveChangesAsync();
            db.Loans.Add(new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
            await db.SaveChangesAsync();
            var service = new MemberService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(member.Id));
        }
    }
}
