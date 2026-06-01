using System;
using System.Linq;
using System.Threading.Tasks;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.BlackBoxTests
{
    public class ReportBbtTests
    {
        private static async Task SeedAsync(LibraryDbContext db)
        {
            var refactoring = new Book { Title = "Refactoring", Author = "Martin Fowler", ISBN = "9780201485677", AvailableCopies = 5 };
            var mythicalMan = new Book { Title = "The Mythical Man-Month", Author = "Fred Brooks", ISBN = "9780201835953", AvailableCopies = 5 };
            var grace = new Member { Name = "Grace Hopper", Email = "grace@navy.mil" };
            var alan = new Member { Name = "Alan Turing", Email = "alan@bletchley.uk" };
            db.Books.AddRange(refactoring, mythicalMan);
            db.Members.AddRange(grace, alan);
            await db.SaveChangesAsync();

            db.Loans.AddRange(
                new Loan { BookId = refactoring.Id, MemberId = grace.Id, LoanDate = DateTime.Today.AddDays(-5), DueDate = DateTime.Today.AddDays(9) },
                new Loan { BookId = mythicalMan.Id, MemberId = alan.Id, LoanDate = DateTime.Today.AddDays(-30), DueDate = DateTime.Today.AddDays(-5) },
                new Loan { BookId = refactoring.Id, MemberId = alan.Id, LoanDate = DateTime.Today.AddDays(-20), DueDate = DateTime.Today.AddDays(-6), ReturnDate = DateTime.Today.AddDays(-10) });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task Report_All_ReturnsEveryLoan()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, null);

            Assert.Equal(3, report.Items.Count);
        }

        [Theory]
        [InlineData(LoanReportType.Active, "Active")]
        [InlineData(LoanReportType.Overdue, "Overdue")]
        [InlineData(LoanReportType.Returned, "Returned")]
        public async Task Report_FilteredByType_ReturnsOnlyMatchingStatus(LoanReportType type, string expectedStatus)
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, type, null);

            Assert.NotEmpty(report.Items);
            Assert.All(report.Items, item => Assert.Equal(expectedStatus, item.Status));
        }

        [Fact]
        public async Task Report_FromDate_ExcludesEarlierLoans()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(DateTime.Today.AddDays(-10), null, LoanReportType.All, null);

            Assert.Single(report.Items);
            Assert.All(report.Items, item => Assert.True(item.LoanDate.Date >= DateTime.Today.AddDays(-10)));
        }

        [Fact]
        public async Task Report_ToDate_ExcludesLaterLoans()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, DateTime.Today.AddDays(-10), LoanReportType.All, null);

            Assert.Equal(2, report.Items.Count);
            Assert.All(report.Items, item => Assert.True(item.LoanDate.Date <= DateTime.Today.AddDays(-10)));
        }

        [Fact]
        public async Task Report_FromDateOnBoundary_IsInclusive()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(DateTime.Today.AddDays(-20), null, LoanReportType.All, null);

            Assert.Equal(2, report.Items.Count);
        }

        [Theory]
        [InlineData("Refactoring", 2)]
        [InlineData("Fowler", 2)]
        [InlineData("Turing", 2)]
        [InlineData("alan@bletchley.uk", 2)]
        [InlineData("no-such-term", 0)]
        public async Task Report_SearchTerm_FiltersAcrossBookAndMemberFields(string term, int expectedCount)
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, term);

            Assert.Equal(expectedCount, report.Items.Count);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Report_BlankSearchTerm_IsIgnored(string? term)
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, term);

            Assert.Equal(3, report.Items.Count);
        }

        [Fact]
        public async Task Report_SummaryCounts_AreComputedFromItems()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, null);

            Assert.Equal(3, report.TotalBooksLoaned);
            Assert.Equal(2, report.UniqueBooksLoaned);
            Assert.Equal(1, report.ActiveLoans);
            Assert.Equal(1, report.OverdueLoans);
            Assert.Equal(1, report.ReturnedLoans);
        }
    }
}
