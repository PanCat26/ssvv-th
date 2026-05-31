using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Tests.Helpers;
using Xunit;

namespace ssvv_th.Tests.FrontendTests
{
    public class LoanFrontendTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public LoanFrontendTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false // We need to check manual 302 redirects
            });
        }

        private async Task SeedDatabaseAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

            // Clean database
            db.Loans.RemoveRange(db.Loans);
            db.Books.RemoveRange(db.Books);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            // Seed Book
            db.Books.Add(new Book
            {
                Title = "The Pragmatic Programmer",
                Author = "Andy Hunt",
                ISBN = "978-0135957059",
                AvailableCopies = 3
            });

            // Seed Member
            db.Members.Add(new Member
            {
                Name = "John Doe",
                Email = "john.doe@example.com",
                Phone = "1234567"
            });

            await db.SaveChangesAsync();
        }

        private async Task<(int loanId, int bookId, int memberId)> SeedLoanAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

            var book = await db.Books.FirstAsync();
            var member = await db.Members.FirstAsync();

            var loan = new Loan
            {
                BookId = book.Id,
                MemberId = member.Id,
                LoanDate = DateTime.Today.AddDays(-5),
                DueDate = DateTime.Today.AddDays(9)
            };

            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            return (loan.Id, book.Id, member.Id);
        }

        [Fact]
        public async Task LoanIndexPage_RendersCorrectly_WithHeaders()
        {
            // Arrange
            await SeedDatabaseAsync();

            // Act
            var response = await _client.GetAsync("/Loan");

            // Assert
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify headers
            var heading = document.QuerySelector("h1");
            Assert.NotNull(heading);
            Assert.Contains("Loans", heading.TextContent);

            // Verify existence of Create Link
            var createLink = document.QuerySelector("a[href='/Loan/Create']");
            Assert.NotNull(createLink);
        }

        [Fact]
        public async Task LoanCreatePage_GET_RendersFormWithDropdowns()
        {
            // Arrange
            await SeedDatabaseAsync();

            // Act
            var response = await _client.GetAsync("/Loan/Create");

            // Assert
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify form fields
            var form = document.QuerySelector("form");
            Assert.NotNull(form);

            var bookSelect = document.QuerySelector("select[name='BookId']");
            Assert.NotNull(bookSelect);
            Assert.Contains("The Pragmatic Programmer", bookSelect.TextContent);

            var memberSelect = document.QuerySelector("select[name='MemberId']");
            Assert.NotNull(memberSelect);
            Assert.Contains("John Doe", memberSelect.TextContent);

            var loanDateInput = document.QuerySelector("input[name='LoanDate']");
            Assert.NotNull(loanDateInput);
        }

        [Fact]
        public async Task LoanCreate_SubmitWithInvalidDates_RendersValidationErrorInGUI()
        {
            // Arrange
            await SeedDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var book = await db.Books.FirstAsync();
            var member = await db.Members.FirstAsync();

            // GET the form to fetch anti-forgery tokens
            var getResponse = await _client.GetAsync("/Loan/Create");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            // Create form payload with reversing dates
            var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Loan/Create");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "BookId", book.Id.ToString() },
                { "MemberId", member.Id.ToString() },
                { "LoanDate", "2026-06-15" },
                { "DueDate", "2026-06-10" }, // DueDate before LoanDate
                { "ReturnDate", "" }
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            // When validation fails, the controller returns View(loan) with a 200 OK (not a redirect)
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify validation span message is displayed in HTML
            var validationSpan = document.QuerySelector("span[data-valmsg-for='DueDate']");
            Assert.NotNull(validationSpan);
            Assert.Contains("Due date cannot be before the loan date.", html);
        }

        [Fact]
        public async Task LoanCreate_SubmitValidData_RedirectsToIndexWithSuccessBanner()
        {
            // Arrange
            await SeedDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            var book = await db.Books.FirstAsync();
            var member = await db.Members.FirstAsync();

            var getResponse = await _client.GetAsync("/Loan/Create");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Loan/Create");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "BookId", book.Id.ToString() },
                { "MemberId", member.Id.ToString() },
                { "LoanDate", DateTime.Today.ToString("yyyy-MM-dd") },
                { "DueDate", DateTime.Today.AddDays(14).ToString("yyyy-MM-dd") },
                { "ReturnDate", "" }
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            // Successful creation redirects to Index
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Loan", response.Headers.Location?.OriginalString);

            // Follow redirect and check success banner in HTML view
            var followResponse = await _client.GetAsync(response.Headers.Location);
            var html = await followResponse.Content.ReadAsStringAsync();

            Assert.Contains("Loan created successfully.", html);
        }

        [Fact]
        public async Task LoanEditPage_GET_RendersFormWithPrepopulatedData()
        {
            // Arrange
            await SeedDatabaseAsync();
            var (loanId, bookId, memberId) = await SeedLoanAsync();

            // Act
            var response = await _client.GetAsync($"/Loan/Edit/{loanId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify input fields contain populated database data
            var bookSelect = document.QuerySelector("select[name='BookId']") as IHtmlSelectElement;
            Assert.NotNull(bookSelect);
            Assert.Equal(bookId.ToString(), bookSelect.Value);

            var memberSelect = document.QuerySelector("select[name='MemberId']") as IHtmlSelectElement;
            Assert.NotNull(memberSelect);
            Assert.Equal(memberId.ToString(), memberSelect.Value);

            var loanDateInput = document.QuerySelector("input[name='LoanDate']") as IHtmlInputElement;
            Assert.NotNull(loanDateInput);
            Assert.Equal(DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd"), loanDateInput.Value);
        }

        [Fact]
        public async Task LoanEdit_SubmitValidData_RedirectsToIndexWithSuccessBanner()
        {
            // Arrange
            await SeedDatabaseAsync();
            var (loanId, bookId, memberId) = await SeedLoanAsync();

            var getResponse = await _client.GetAsync($"/Loan/Edit/{loanId}");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/Loan/Edit/{loanId}");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Id", loanId.ToString() },
                { "BookId", bookId.ToString() },
                { "MemberId", memberId.ToString() },
                { "LoanDate", DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd") },
                { "DueDate", DateTime.Today.AddDays(9).ToString("yyyy-MM-dd") },
                { "ReturnDate", DateTime.Today.ToString("yyyy-MM-dd") } // Setting ReturnDate
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Loan", response.Headers.Location?.OriginalString);

            var followResponse = await _client.GetAsync(response.Headers.Location);
            var html = await followResponse.Content.ReadAsStringAsync();

            Assert.Contains("Loan updated successfully.", html);
        }

        [Fact]
        public async Task LoanEdit_SubmitInvalidDates_RendersValidationErrorInGUI()
        {
            // Arrange
            await SeedDatabaseAsync();
            var (loanId, bookId, memberId) = await SeedLoanAsync();

            var getResponse = await _client.GetAsync($"/Loan/Edit/{loanId}");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/Loan/Edit/{loanId}");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Id", loanId.ToString() },
                { "BookId", bookId.ToString() },
                { "MemberId", memberId.ToString() },
                { "LoanDate", DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd") },
                { "DueDate", DateTime.Today.AddDays(-10).ToString("yyyy-MM-dd") }, // Invalid: DueDate before LoanDate
                { "ReturnDate", "" }
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            var validationSpan = document.QuerySelector("span[data-valmsg-for='DueDate']");
            Assert.NotNull(validationSpan);
            Assert.Contains("Due date cannot be before the loan date.", html);
        }

        [Fact]
        public async Task LoanDeletePage_GET_RendersConfirmationDetails()
        {
            // Arrange
            await SeedDatabaseAsync();
            var (loanId, _, _) = await SeedLoanAsync();

            // Act
            var response = await _client.GetAsync($"/Loan/Delete/{loanId}");

            // Assert
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify confirmation heading
            var heading = document.QuerySelector("h1");
            Assert.NotNull(heading);
            Assert.Contains("Delete Loan", heading.TextContent);

            // Verify confirmation prompts
            Assert.Contains("Are you sure you want to delete this loan?", html);
            Assert.Contains("The Pragmatic Programmer", html);
            Assert.Contains("John Doe", html);
        }

        [Fact]
        public async Task LoanDelete_SubmitConfirmation_RedirectsToIndexWithSuccessBanner()
        {
            // Arrange
            await SeedDatabaseAsync();
            var (loanId, _, _) = await SeedLoanAsync();

            var getResponse = await _client.GetAsync($"/Loan/Delete/{loanId}");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            var postRequest = new HttpRequestMessage(HttpMethod.Post, $"/Loan/Delete/{loanId}");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token }
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Loan", response.Headers.Location?.OriginalString);

            var followResponse = await _client.GetAsync(response.Headers.Location);
            var html = await followResponse.Content.ReadAsStringAsync();

            Assert.Contains("Loan deleted successfully.", html);
        }

        [Fact]
        public async Task LoanCreate_SubmitWithUnavailableBook_RendersValidationErrorInGUI()
        {
            // Arrange
            await SeedDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

            // Seed an out of stock book
            var outOfStockBook = new Book
            {
                Title = "Out of Stock Book",
                Author = "Author",
                ISBN = "999-9999",
                AvailableCopies = 0 // 0 copies
            };
            db.Books.Add(outOfStockBook);
            await db.SaveChangesAsync();

            var member = await db.Members.FirstAsync();

            // GET the form to fetch anti-forgery tokens
            var getResponse = await _client.GetAsync("/Loan/Create");
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(getResponse);

            // POST payload with out of stock BookId
            var postRequest = new HttpRequestMessage(HttpMethod.Post, "/Loan/Create");
            postRequest.Headers.Add("Cookie", cookie);

            var formData = new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "BookId", outOfStockBook.Id.ToString() },
                { "MemberId", member.Id.ToString() },
                { "LoanDate", DateTime.Today.ToString("yyyy-MM-dd") },
                { "DueDate", DateTime.Today.AddDays(14).ToString("yyyy-MM-dd") },
                { "ReturnDate", "" }
            };

            postRequest.Content = new FormUrlEncodedContent(formData);

            // Act
            var response = await _client.SendAsync(postRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));

            // Verify out-of-stock validation message is displayed in HTML
            var validationSpan = document.QuerySelector("span[data-valmsg-for='BookId']");
            Assert.NotNull(validationSpan);
            Assert.Contains("This book is currently unavailable.", html);
        }
    }
}
