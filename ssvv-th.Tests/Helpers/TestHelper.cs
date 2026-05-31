using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Html.Dom;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ssvv_th.Data;

namespace ssvv_th.Tests.Helpers
{
    public class CustomWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Remove the app's real LibraryDbContext registration.
                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<LibraryDbContext>));

                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                // Add an In-Memory Database for testing, ignoring transaction warnings.
                services.AddDbContext<LibraryDbContext>(options =>
                {
                    options.UseInMemoryDatabase("InMemoryDbForTesting")
                           .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        }
    }

    public static class TestHelper
    {
        // Extracts the anti-forgery token and the anti-forgery cookie from a GET response.
        public static async Task<(string token, string cookie)> ExtractAntiforgeryTokenAndCookie(HttpResponseMessage getResponse)
        {
            getResponse.EnsureSuccessStatusCode();

            // 1. Extract the cookie.
            string cookie = string.Empty;
            if (getResponse.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                var antiforgeryCookie = cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Antiforgery"));
                if (antiforgeryCookie != null)
                {
                    // Get only the key-value pair part.
                    cookie = antiforgeryCookie.Split(';').FirstOrDefault() ?? string.Empty;
                }
            }

            // 2. Extract the __RequestVerificationToken input from the HTML.
            var html = await getResponse.Content.ReadAsStringAsync();
            var context = BrowsingContext.New(Configuration.Default);
            var document = await context.OpenAsync(req => req.Content(html));
            var tokenInput = document.QuerySelector("input[name='__RequestVerificationToken']") as IHtmlInputElement;

            var token = tokenInput?.Value ?? string.Empty;

            return (token, cookie);
        }
    }
}
