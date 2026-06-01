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

namespace ssvv_th.Tests.GuiTests
{
    [Collection("GuiWebCollection")]
    public class BookGuiTests
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public BookGuiTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        private async Task<int> ResetAndSeedBookAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            db.Loans.RemoveRange(db.Loans);
            db.Books.RemoveRange(db.Books);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            var book = new Book { Title = "The Pragmatic Programmer", Author = "Andy Hunt", ISBN = "978-0135957059", AvailableCopies = 3 };
            db.Books.Add(book);
            await db.SaveChangesAsync();
            return book.Id;
        }

        private static async Task<IHtmlDocument> ParseAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            return (IHtmlDocument)await context.OpenAsync(req => req.Content(html));
        }

        [Fact]
        public async Task Index_RendersHeaderAddLinkAndSeededRow()
        {
            await ResetAndSeedBookAsync();

            var response = await _client.GetAsync("/Book");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            Assert.Contains("Books", document.QuerySelector("h1")!.TextContent);
            Assert.NotNull(document.QuerySelector("a[href='/Book/Create']"));
            Assert.Contains("The Pragmatic Programmer", document.Body!.TextContent);
        }

        [Fact]
        public async Task Create_Get_RendersAllFormFields()
        {
            await ResetAndSeedBookAsync();

            var response = await _client.GetAsync("/Book/Create");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            Assert.NotNull(document.QuerySelector("input[name='Title']"));
            Assert.NotNull(document.QuerySelector("input[name='Author']"));
            Assert.NotNull(document.QuerySelector("input[name='ISBN']"));
            Assert.NotNull(document.QuerySelector("input[name='AvailableCopies']"));
        }

        [Fact]
        public async Task Create_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            await ResetAndSeedBookAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync("/Book/Create"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/Book/Create");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Title", "Refactoring" },
                { "Author", "Martin Fowler" },
                { "ISBN", "9780201485677" },
                { "AvailableCopies", "4" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Book")).Content.ReadAsStringAsync();
            Assert.Contains("Book created successfully.", followHtml);
            Assert.Contains("Refactoring", followHtml);
        }

        [Fact]
        public async Task Create_PostMissingTitle_RendersValidationError()
        {
            await ResetAndSeedBookAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync("/Book/Create"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/Book/Create");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Title", "" },
                { "Author", "Martin Fowler" },
                { "ISBN", "9780201485677" },
                { "AvailableCopies", "4" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Title is required.", html);
        }

        [Fact]
        public async Task Edit_Get_RendersPrepopulatedValues()
        {
            var bookId = await ResetAndSeedBookAsync();

            var response = await _client.GetAsync($"/Book/Edit/{bookId}");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            var titleInput = document.QuerySelector("input[name='Title']") as IHtmlInputElement;
            Assert.NotNull(titleInput);
            Assert.Equal("The Pragmatic Programmer", titleInput!.Value);
        }

        [Fact]
        public async Task Edit_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            var bookId = await ResetAndSeedBookAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Book/Edit/{bookId}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Book/Edit/{bookId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Id", bookId.ToString() },
                { "Title", "The Pragmatic Programmer, 2nd Edition" },
                { "Author", "Andy Hunt" },
                { "ISBN", "978-0135957059" },
                { "AvailableCopies", "5" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Book")).Content.ReadAsStringAsync();
            Assert.Contains("Book updated successfully.", followHtml);
            Assert.Contains("2nd Edition", followHtml);
        }

        [Fact]
        public async Task Delete_Get_RendersConfirmationDetails()
        {
            var bookId = await ResetAndSeedBookAsync();

            var response = await _client.GetAsync($"/Book/Delete/{bookId}");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Are you sure you want to delete this book?", html);
            Assert.Contains("The Pragmatic Programmer", html);
        }

        [Fact]
        public async Task Delete_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            var bookId = await ResetAndSeedBookAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Book/Delete/{bookId}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Book/Delete/{bookId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Book")).Content.ReadAsStringAsync();
            Assert.Contains("Book deleted successfully.", followHtml);
        }

        [Fact]
        public async Task Delete_PostWhenReferencedByLoan_ShowsErrorBanner()
        {
            var bookId = await ResetAndSeedBookAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                var member = new Member { Name = "Holder", Email = "holder@example.com" };
                db.Members.Add(member);
                await db.SaveChangesAsync();
                db.Loans.Add(new Loan { BookId = bookId, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
                await db.SaveChangesAsync();
            }

            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Book/Delete/{bookId}"));
            var request = new HttpRequestMessage(HttpMethod.Post, $"/Book/Delete/{bookId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", token } });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Book")).Content.ReadAsStringAsync();
            Assert.Contains("cannot be deleted", followHtml);
        }
    }
}
