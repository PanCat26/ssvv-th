using System;
using System.Linq;
using System.Threading.Tasks;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.WhiteBoxTests
{
    public class ReportWbtTests
    {
        private static async Task SeedAsync(LibraryDbContext db)
        {
            var alpha = new Book { Title = "Alpha Patterns", Author = "Anders Berg", ISBN = "AAA", AvailableCopies = 5 };
            var zeta = new Book { Title = "Zeta Systems", Author = "Zane Cooper", ISBN = "ZZZ", AvailableCopies = 5 };
            var maria = new Member { Name = "Maria Garcia", Email = "maria@uni.edu" };
            var noah = new Member { Name = "Noah Kim", Email = "noah@corp.io" };
            db.Books.AddRange(alpha, zeta);
            db.Members.AddRange(maria, noah);
            await db.SaveChangesAsync();

            db.Loans.AddRange(
                new Loan { BookId = alpha.Id, MemberId = maria.Id, LoanDate = DateTime.Today.AddDays(-3), DueDate = DateTime.Today.AddDays(5) },
                new Loan { BookId = zeta.Id, MemberId = noah.Id, LoanDate = DateTime.Today.AddDays(-40), DueDate = DateTime.Today.AddDays(-2) },
                new Loan { BookId = alpha.Id, MemberId = noah.Id, LoanDate = DateTime.Today.AddDays(-15), DueDate = DateTime.Today.AddDays(-1), ReturnDate = DateTime.Today.AddDays(-3) });
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task NoFilters_DefaultArm_ReturnsAllOrderedByDueDate()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, null);

            Assert.Equal(3, report.Items.Count);
            var dueDates = report.Items.Select(i => i.DueDate).ToList();
            Assert.Equal(dueDates.OrderBy(d => d).ToList(), dueDates);
        }

        [Fact]
        public async Task BothDateBounds_NarrowToRange()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(DateTime.Today.AddDays(-10), DateTime.Today.AddDays(10), LoanReportType.All, null);

            Assert.Single(report.Items);
        }

        [Theory]
        [InlineData(LoanReportType.Active, "Active", 1)]
        [InlineData(LoanReportType.Overdue, "Overdue", 1)]
        [InlineData(LoanReportType.Returned, "Returned", 1)]
        public async Task EachSwitchArm_FiltersByStatus(LoanReportType type, string expectedStatus, int expectedCount)
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, type, null);

            Assert.Equal(expectedCount, report.Items.Count);
            Assert.All(report.Items, item => Assert.Equal(expectedStatus, item.Status));
        }

        [Theory]
        [InlineData("Alpha", 2)]
        [InlineData("Zane", 1)]
        [InlineData("Maria", 1)]
        [InlineData("noah@corp.io", 2)]
        public async Task SearchTerm_CoversEachMatchableField(string term, int expectedCount)
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, term);

            Assert.Equal(expectedCount, report.Items.Count);
        }

        [Fact]
        public async Task WhitespaceSearchTerm_SkipsFilterBranch()
        {
            using var db = InMemoryDb.Create();
            await SeedAsync(db);
            var service = new ReportService(db);

            var report = await service.GenerateLoanReportAsync(null, null, LoanReportType.All, "   ");

            Assert.Equal(3, report.Items.Count);
        }
    }
}
