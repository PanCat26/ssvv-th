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

namespace ssvv_th.Tests.FrontendTests
{
    // A custom WebApplicationFactory that boots a real Kestrel TCP server on a free port
    public class KestrelWebApplicationFactory<TProgram> : WebApplicationFactory<TProgram> where TProgram : class
    {
        private IHost? _kestrelHost;
        private string _serverAddress = "";

        public string ServerAddress
        {
            get
            {
                if (string.IsNullOrEmpty(_serverAddress))
                {
                    // Accessing Services forces host creation and startup of the test host
                    _ = Services;
                }
                return _serverAddress;
            }
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // 1. Build and start the test host (TestServer) so WebApplicationFactory is happy
            var testHost = builder.Build();
            testHost.Start();

            // 2. Build and start a completely separate IHost for Kestrel using the same DB configuration
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
                            options.UseInMemoryDatabase("PlaywrightDbForTesting")
                                   .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                        });
                        services.AddScoped<IBookService, BookService>();
                        services.AddScoped<IMemberService, MemberService>();
                        services.AddScoped<ILoanService, LoanService>();
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

            // Extract the dynamically assigned address
            var server = _kestrelHost.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
            _serverAddress = addresses?.FirstOrDefault()?.TrimEnd('/') ?? "http://127.0.0.1:5223";

            return testHost;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<LibraryDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<LibraryDbContext>(options =>
                {
                    options.UseInMemoryDatabase("PlaywrightDbForTesting")
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

    public class LoanPlaywrightTests : IClassFixture<KestrelWebApplicationFactory<Program>>
    {
        private readonly KestrelWebApplicationFactory<Program> _factory;

        public LoanPlaywrightTests(KestrelWebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task LoanCreate_ViaPlaywrightBrowser_PerformsButtonClickAndFormSubmission()
        {
            // Arrange
            var serverAddress = _factory.ServerAddress;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                db.Loans.RemoveRange(db.Loans);
                db.Books.RemoveRange(db.Books);
                db.Members.RemoveRange(db.Members);
                await db.SaveChangesAsync();

                var book = new Book { Title = "Playwright testing", Author = "QA Team", ISBN = "999-888", AvailableCopies = 2 };
                var member = new Member { Name = "John Smith", Email = "john@example.com" };
                db.Books.Add(book);
                db.Members.Add(member);
                await db.SaveChangesAsync();
            }

            // Launch headless Playwright Browser
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync();

            // Act
            // 1. Navigate to the dynamic creation page URL
            await page.GotoAsync($"{serverAddress}/Loan/Create");

            // 2. Select Book and Member options from the dropdowns using labels
            try
            {
                await page.SelectOptionAsync("select[name='BookId']", new[] { new SelectOptionValue { Label = "Playwright testing (QA Team) - 2 available" } });
                await page.SelectOptionAsync("select[name='MemberId']", new[] { new SelectOptionValue { Label = "John Smith (john@example.com)" } });
            }
            catch (Exception ex)
            {
                var content = await page.ContentAsync();
                throw new Exception($"Failed to interact with form on page. Current URL: {page.Url}. Page content: {content}", ex);
            }

            // 3. Click the Save button (actual browser mouse click!)
            await page.ClickAsync("button[type='submit']");

            // Assert
            // 4. Verify browser navigation/URL redirect to Index page
            await page.WaitForURLAsync($"{serverAddress}/Loan");
            Assert.Equal($"{serverAddress}/Loan", page.Url);

            // 5. Verify the success message is rendered in the GUI DOM
            var successBanner = await page.Locator(".alert-success").TextContentAsync();
            Assert.Contains("Loan created successfully.", successBanner);
        }

        [Fact]
        public async Task LoanCreate_InvalidDates_RendersValidationErrorInBrowser()
        {
            // Arrange
            var serverAddress = _factory.ServerAddress;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                db.Loans.RemoveRange(db.Loans);
                db.Books.RemoveRange(db.Books);
                db.Members.RemoveRange(db.Members);
                await db.SaveChangesAsync();

                var book = new Book { Title = "Playwright validation testing", Author = "QA Team", ISBN = "777-666", AvailableCopies = 2 };
                var member = new Member { Name = "Jane Doe", Email = "jane@example.com" };
                db.Books.Add(book);
                db.Members.Add(member);
                await db.SaveChangesAsync();
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            // Act
            await page.GotoAsync($"{serverAddress}/Loan/Create");

            // Select book and member
            await page.SelectOptionAsync("select[name='BookId']", new[] { new SelectOptionValue { Label = "Playwright validation testing (QA Team) - 2 available" } });
            await page.SelectOptionAsync("select[name='MemberId']", new[] { new SelectOptionValue { Label = "Jane Doe (jane@example.com)" } });

            // Set DueDate before LoanDate (DueDate is 5 days ago)
            await page.FillAsync("input[name='DueDate']", DateTime.Today.AddDays(-5).ToString("yyyy-MM-dd"));

            // Submit
            await page.ClickAsync("button[type='submit']");

            // Assert
            // 1. Verify we stay on the Create page (contains /Loan/Create)
            Assert.Contains("/Loan/Create", page.Url);

            // 2. Verify validation error message is rendered in the HTML DOM
            var validationError = await page.Locator("span[data-valmsg-for='DueDate']").TextContentAsync();
            Assert.Contains("Due date cannot be before the loan date.", validationError);
        }

        [Fact]
        public async Task LoanEdit_ViaPlaywrightBrowser_ReturnsSuccessAlert()
        {
            // Arrange
            var serverAddress = _factory.ServerAddress;
            int loanId;

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                db.Loans.RemoveRange(db.Loans);
                db.Books.RemoveRange(db.Books);
                db.Members.RemoveRange(db.Members);
                await db.SaveChangesAsync();

                var book = new Book { Title = "Playwright edit testing", Author = "QA Team", ISBN = "555-444", AvailableCopies = 2 };
                var member = new Member { Name = "Alice Cooper", Email = "alice@example.com" };
                db.Books.Add(book);
                db.Members.Add(member);
                await db.SaveChangesAsync();

                var loan = new Loan
                {
                    BookId = book.Id,
                    MemberId = member.Id,
                    LoanDate = DateTime.Today.AddDays(-5),
                    DueDate = DateTime.Today.AddDays(9)
                };
                db.Loans.Add(loan);
                await db.SaveChangesAsync();
                loanId = loan.Id;
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            // Act
            await page.GotoAsync($"{serverAddress}/Loan/Edit/{loanId}");

            // Fill ReturnDate
            await page.FillAsync("input[name='ReturnDate']", DateTime.Today.ToString("yyyy-MM-dd"));

            // Submit
            await page.ClickAsync("button[type='submit']");

            // Assert
            // 1. Verify redirect to /Loan
            await page.WaitForURLAsync($"{serverAddress}/Loan");
            Assert.Equal($"{serverAddress}/Loan", page.Url);

            // 2. Verify success banner is rendered
            var successBanner = await page.Locator(".alert-success").TextContentAsync();
            Assert.Contains("Loan updated successfully.", successBanner);
        }

        [Fact]
        public async Task LoanIndex_ViaPlaywrightBrowser_DisplaysData()
        {
            var serverAddress = _factory.ServerAddress;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                db.Loans.RemoveRange(db.Loans);
                db.Books.RemoveRange(db.Books);
                db.Members.RemoveRange(db.Members);
                await db.SaveChangesAsync();

                var book = new Book { Title = "Playwright Read Book", Author = "QA Team", ISBN = "read-123", AvailableCopies = 2 };
                var member = new Member { Name = "Reader", Email = "read@example.com" };
                db.Books.Add(book);
                db.Members.Add(member);
                await db.SaveChangesAsync();

                db.Loans.Add(new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) });
                await db.SaveChangesAsync();
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{serverAddress}/Loan");

            var tableText = await page.Locator("table").TextContentAsync();
            Assert.Contains("Playwright Read Book", tableText);
            Assert.Contains("Reader", tableText);
        }

        [Fact]
        public async Task LoanDelete_ViaPlaywrightBrowser_RemovesRow()
        {
            var serverAddress = _factory.ServerAddress;
            int loanId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                db.Loans.RemoveRange(db.Loans);
                db.Books.RemoveRange(db.Books);
                db.Members.RemoveRange(db.Members);
                await db.SaveChangesAsync();

                var book = new Book { Title = "Playwright Delete Book", Author = "QA Team", ISBN = "del-123", AvailableCopies = 2 };
                var member = new Member { Name = "Deleter", Email = "del@example.com" };
                db.Books.Add(book);
                db.Members.Add(member);
                await db.SaveChangesAsync();

                var loan = new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) };
                db.Loans.Add(loan);
                await db.SaveChangesAsync();
                loanId = loan.Id;
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            await page.GotoAsync($"{serverAddress}/Loan/Delete/{loanId}");
            await page.ClickAsync("button.btn-danger");

            await page.WaitForURLAsync($"{serverAddress}/Loan");
            
            var successBanner = await page.Locator(".alert-success").TextContentAsync();
            Assert.Contains("Loan deleted successfully.", successBanner);
        }
    }
}
