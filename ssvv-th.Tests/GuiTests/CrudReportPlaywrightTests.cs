using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.GuiTests
{
    public class CrudKestrelWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        private const string DbName = "CrudReportPlaywrightDb";
        private IHost? _kestrelHost;
        private string _serverAddress = "";

        public string ServerAddress
        {
            get
            {
                if (string.IsNullOrEmpty(_serverAddress))
                {
                    _ = Services;
                }
                return _serverAddress;
            }
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var testHost = builder.Build();
            testHost.Start();

            _kestrelHost = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://127.0.0.1:0");
                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddControllersWithViews()
                                .AddApplicationPart(typeof(Program).Assembly);
                        services.AddDbContext<LibraryDbContext>(options =>
                        {
                            options.UseInMemoryDatabase(DbName)
                                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                        });
                        services.AddScoped<IBookService, BookService>();
                        services.AddScoped<IMemberService, MemberService>();
                        services.AddScoped<ILoanService, LoanService>();
                        services.AddScoped<IReportService, ReportService>();
                    });
                    webBuilder.Configure(app =>
                    {
                        app.UseStaticFiles();
                        app.UseRouting();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapControllerRoute(
                                name: "default",
                                pattern: "{controller=Book}/{action=Index}/{id?}");
                        });
                    });
                })
                .Build();

            _kestrelHost.Start();

            var server = _kestrelHost.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            _serverAddress = addresses?.FirstOrDefault()?.TrimEnd('/') ?? "http://127.0.0.1:5223";

            return testHost;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<LibraryDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<LibraryDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DbName)
                           .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        }

        protected override void Dispose(bool disposing)
        {
            _kestrelHost?.Dispose();
            base.Dispose(disposing);
        }
    }

    public class CrudReportPlaywrightTests : IClassFixture<CrudKestrelWebApplicationFactory<Program>>
    {
        private readonly CrudKestrelWebApplicationFactory<Program> _factory;

        public CrudReportPlaywrightTests(CrudKestrelWebApplicationFactory<Program> factory)
        {
            _factory = factory;
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

        [Fact]
        public async Task BookCreate_ViaBrowser_RedirectsWithSuccessBanner()
        {
            await ResetAsync();
            var serverAddress = _factory.ServerAddress;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{serverAddress}/Book/Create");
            await page.FillAsync("input[name='Title']", "Refactoring");
            await page.FillAsync("input[name='Author']", "Martin Fowler");
            await page.FillAsync("input[name='ISBN']", "9780201485677");
            await page.FillAsync("input[name='AvailableCopies']", "4");
            await page.ClickAsync("button[type='submit']");

            await page.WaitForSelectorAsync(".alert-success");
            var banner = await page.Locator(".alert-success").TextContentAsync();
            Assert.Contains("Book created successfully.", banner);
        }

        [Fact]
        public async Task MemberCreate_ViaBrowser_RedirectsWithSuccessBanner()
        {
            await ResetAsync();
            var serverAddress = _factory.ServerAddress;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{serverAddress}/Member/Create");
            await page.FillAsync("input[name='Name']", "Grace Hopper");
            await page.FillAsync("input[name='Email']", "grace@navy.mil");
            await page.FillAsync("input[name='Phone']", "0700111222");
            await page.ClickAsync("button[type='submit']");

            await page.WaitForSelectorAsync(".alert-success");
            var banner = await page.Locator(".alert-success").TextContentAsync();
            Assert.Contains("Member created successfully.", banner);
        }

        [Fact]
        public async Task Report_FilterByReturned_ViaBrowser_ShowsOnlyReturned()
        {
            await ResetAsync();
            using (var scope = _factory.Services.CreateScope())
            {
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

            var serverAddress = _factory.ServerAddress;
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{serverAddress}/Report");
            await page.SelectOptionAsync("select[name='ReportType']", new[] { new SelectOptionValue { Label = "Returned" } });
            await page.ClickAsync("button[type='submit']");
            await page.WaitForURLAsync($"**ReportType={(int)LoanReportType.Returned}**");

            var body = await page.Locator("body").TextContentAsync();
            Assert.Contains("Returned Reading", body);
            Assert.DoesNotContain("Active Reading", body);
        }
    }
}
