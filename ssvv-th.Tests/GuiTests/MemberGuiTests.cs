using System;
using System.Collections.Generic;
using System.Net;
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
    public class MemberGuiTests
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;

        public MemberGuiTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
        }

        private async Task<int> ResetAndSeedMemberAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
            db.Loans.RemoveRange(db.Loans);
            db.Books.RemoveRange(db.Books);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            var member = new Member { Name = "John Doe", Email = "john.doe@example.com", Phone = "0712345678" };
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return member.Id;
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
            await ResetAndSeedMemberAsync();

            var response = await _client.GetAsync("/Member");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            Assert.Contains("Members", document.QuerySelector("h1")!.TextContent);
            Assert.NotNull(document.QuerySelector("a[href='/Member/Create']"));
            Assert.Contains("john.doe@example.com", document.Body!.TextContent);
        }

        [Fact]
        public async Task Create_Get_RendersAllFormFields()
        {
            await ResetAndSeedMemberAsync();

            var response = await _client.GetAsync("/Member/Create");

            response.EnsureSuccessStatusCode();
            var document = await ParseAsync(response);
            Assert.NotNull(document.QuerySelector("input[name='Name']"));
            Assert.NotNull(document.QuerySelector("input[name='Email']"));
            Assert.NotNull(document.QuerySelector("input[name='Phone']"));
        }

        [Fact]
        public async Task Create_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            await ResetAndSeedMemberAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync("/Member/Create"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/Member/Create");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Name", "Grace Hopper" },
                { "Email", "grace@navy.mil" },
                { "Phone", "0700111222" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/Member", response.Headers.Location?.OriginalString);
            var followHtml = await (await _client.GetAsync("/Member")).Content.ReadAsStringAsync();
            Assert.Contains("Member created successfully.", followHtml);
            Assert.Contains("Grace Hopper", followHtml);
        }

        [Fact]
        public async Task Create_PostInvalidEmail_RendersValidationError()
        {
            await ResetAndSeedMemberAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync("/Member/Create"));

            var request = new HttpRequestMessage(HttpMethod.Post, "/Member/Create");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Name", "Bad Email User" },
                { "Email", "not-an-email" },
                { "Phone", "0700111222" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Invalid email address.", html);
        }

        [Fact]
        public async Task Edit_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            var memberId = await ResetAndSeedMemberAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Member/Edit/{memberId}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Member/Edit/{memberId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "__RequestVerificationToken", token },
                { "Id", memberId.ToString() },
                { "Name", "Johnathan Doe" },
                { "Email", "john.doe@example.com" },
                { "Phone", "0712345678" }
            });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Member")).Content.ReadAsStringAsync();
            Assert.Contains("Member updated successfully.", followHtml);
            Assert.Contains("Johnathan Doe", followHtml);
        }

        [Fact]
        public async Task Delete_Get_RendersConfirmationDetails()
        {
            var memberId = await ResetAndSeedMemberAsync();

            var response = await _client.GetAsync($"/Member/Delete/{memberId}");

            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("Are you sure you want to delete this member?", html);
            Assert.Contains("John Doe", html);
        }

        [Fact]
        public async Task Delete_PostValid_RedirectsToIndexWithSuccessBanner()
        {
            var memberId = await ResetAndSeedMemberAsync();
            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Member/Delete/{memberId}"));

            var request = new HttpRequestMessage(HttpMethod.Post, $"/Member/Delete/{memberId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", token } });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Member")).Content.ReadAsStringAsync();
            Assert.Contains("Member deleted successfully.", followHtml);
        }

        [Fact]
        public async Task Delete_PostWhenReferencedByLoan_ShowsErrorBanner()
        {
            var memberId = await ResetAndSeedMemberAsync();
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                var book = new Book { Title = "Book", Author = "Author", ISBN = "123", AvailableCopies = 2 };
                db.Books.Add(book);
                await db.SaveChangesAsync();
                db.Loans.Add(new Loan { BookId = book.Id, MemberId = memberId, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
                await db.SaveChangesAsync();
            }

            var (token, cookie) = await TestHelper.ExtractAntiforgeryTokenAndCookie(await _client.GetAsync($"/Member/Delete/{memberId}"));
            var request = new HttpRequestMessage(HttpMethod.Post, $"/Member/Delete/{memberId}");
            request.Headers.Add("Cookie", cookie);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { { "__RequestVerificationToken", token } });

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var followHtml = await (await _client.GetAsync("/Member")).Content.ReadAsStringAsync();
            Assert.Contains("cannot be deleted", followHtml);
        }
    }
}
