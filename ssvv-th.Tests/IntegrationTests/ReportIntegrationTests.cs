using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ssvv_th.Controllers;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.IntegrationTests
{
    public class ReportIntegrationTests
    {
        private static async Task SeedAsync(Data.LibraryDbContext db)
        {
            var book = new Book { Title = "Test-Driven Development", Author = "Kent Beck", ISBN = "9780321146533", AvailableCopies = 5 };
            var other = new Book { Title = "Continuous Delivery", Author = "Jez Humble", ISBN = "9780321601919", AvailableCopies = 5 };
            var member = new Member { Name = "Linus Torvalds", Email = "linus@kernel.org" };
            db.Books.AddRange(book, other);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            db.Loans.AddRange(
                new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today.AddDays(-2), DueDate = DateTime.Today.AddDays(12) },
                new Loan { BookId = other.Id, MemberId = member.Id, LoanDate = DateTime.Today.AddDays(-20), DueDate = DateTime.Today.AddDays(-6), ReturnDate = DateTime.Today.AddDays(-8) });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Index_ReturnsViewModelPopulatedFromDatabase()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var controller = new ReportController(new ReportService(db));

            var result = await controller.Index();

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<LoanReportViewModel>(view.Model);
            Assert.Equal(2, model.Items.Count);
        }

        [Fact]
        public async Task Index_WithReturnedFilter_ReturnsOnlyReturnedLoans()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var controller = new ReportController(new ReportService(db));

            var result = await controller.Index(reportType: LoanReportType.Returned);

            var view = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<LoanReportViewModel>(view.Model);
            Assert.Single(model.Items);
            Assert.Equal("Returned", model.Items[0].Status);
        }

        [Fact]
        public async Task ExportCsv_ReturnsCsvFileWithHeaderAndRows()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var controller = new ReportController(new ReportService(db));

            var result = await controller.ExportCsv();

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("text/csv", file.ContentType);

            var csv = Encoding.UTF8.GetString(file.FileContents);
            Assert.Contains("Loan Id,Book,Author,Member,Email,Loan Date,Due Date,Return Date,Status", csv);
            Assert.Contains("Test-Driven Development", csv);
            Assert.Contains("Continuous Delivery", csv);
        }
    }
}
