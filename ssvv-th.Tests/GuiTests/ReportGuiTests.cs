using System;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.Extensions.DependencyInjection;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.GuiTests
{
    [Collection("GuiWebCollection")]
    public class ReportGuiTests
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public ReportGuiTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task ResetAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            db.Loans.RemoveRange(db.Loans);
            db.Books.RemoveRange(db.Books);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();
        }

        private async Task SeedLoansAsync()
        {
            await ResetAsync();
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

            var activeBook = new Book { Title = "Active Reading", Author = "Active Author", ISBN = "AAA", AvailableCopies = 3 };
            var returnedBook = new Book { Title = "Returned Reading", Author = "Returned Author", ISBN = "RRR", AvailableCopies = 3 };
            var member = new Member { Name = "Report Member", Email = "report@example.com" };
            db.Books.AddRange(activeBook, returnedBook);
            db.Members.Add(member);
            await db.SaveChangesAsync();

            db.Loans.AddRange(
                new Loan { BookId = activeBook.Id, MemberId = member.Id, LoanDate = DateTime.Today.AddDays(-2), DueDate = DateTime.Today.AddDays(10) },
                new Loan { BookId = returnedBook.Id, MemberId = member.Id, LoanDate = DateTime.Today.AddDays(-10), DueDate = DateTime.Today.AddDays(-2), ReturnDate = DateTime.Today.AddDays(-4) });
            await db.SaveChangesAsync();
        }

        private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            return (IHtmlDocument)await context.OpenAsync(req => req.Content(html));
        }

        [Fact]
        public async Task Index_RendersHeaderFilterFormAndRows()
        {
            await SeedLoansAsync();

            var response = await _client.GetAsync("/Report");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            Assert.Contains("Loan Reports", document.QuerySelector("h1")!.TextContent);
            Assert.NotNull(document.QuerySelector("input[name='FromDate']"));
            Assert.NotNull(document.QuerySelector("input[name='ToDate']"));
            Assert.NotNull(document.QuerySelector("select[name='ReportType']"));
            Assert.NotNull(document.QuerySelector("input[name='SearchTerm']"));

            var body = document.Body!.TextContent;
            Assert.Contains("Books Loaned", body);
            Assert.Contains("Active Reading", body);
            Assert.Contains("Returned Reading", body);
        }

        [Fact]
        public async Task Index_FilteredByReturned_ShowsOnlyReturnedRows()
        {
            await SeedLoansAsync();

            var response = await _client.GetAsync("/Report?reportType=Returned");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Returned Reading", html);
            Assert.DoesNotContain("Active Reading", html);
        }

        [Fact]
        public async Task Index_SearchTerm_FiltersRows()
        {
            await SeedLoansAsync();

            var response = await _client.GetAsync("/Report?searchTerm=Active");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Active Reading", html);
            Assert.DoesNotContain("Returned Reading", html);
        }

        [Fact]
        public async Task Index_NoLoans_ShowsEmptyMessage()
        {
            await ResetAsync();

            var response = await _client.GetAsync("/Report");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("No books were loaned for this report.", html);
        }

        [Fact]
        public async Task ExportCsv_ReturnsCsvContent()
        {
            await SeedLoansAsync();

            var response = await _client.GetAsync("/Report/ExportCsv");

            response.EnsureSuccessStatusCode();
            Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
            var csv = await response.Content.ReadAsStringAsync();
            Assert.Contains("Loan Id,Book,Author,Member,Email,Loan Date,Due Date,Return Date,Status", csv);
            Assert.Contains("Active Reading", csv);
        }
    }
}
