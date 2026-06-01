using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;
using ssvv_th.Services;
using Xunit;

namespace ssvv_th.Tests.Services
{
    public class LoanServiceIntegrationTests
    {
        private LibraryDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<LibraryDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            return new LibraryDbContext(options);
        }

        [Fact]
        public async Task FullLifecycle_CreateUpdateDelete_WorksCorrectly()
        {
            var dbName = Guid.NewGuid().ToString();
            
            using (var context = GetDbContext(dbName))
            {
                var book = new Book { Title = "Integration Book", Author = "Author", ISBN = "999", AvailableCopies = 2 };
                var member = new Member { Name = "Integration User", Email = "int@example.com", Phone = "999" };
                context.Books.Add(book);
                context.Members.Add(member);
                await context.SaveChangesAsync();
            }

            int loanId;

            using (var context = GetDbContext(dbName))
            {
                var service = new LoanService(context);
                var book = await context.Books.FirstAsync();
                var member = await context.Members.FirstAsync();

                var loan = new Loan { BookId = book.Id, MemberId = member.Id, LoanDate = DateTime.Today, DueDate = DateTime.Today.AddDays(7) };
                var result = await service.CreateAsync(loan);
                Assert.True(result.Succeeded);
                loanId = result.Loan!.Id;
            }

            using (var context = GetDbContext(dbName))
            {
                var book = await context.Books.FirstAsync();
                Assert.Equal(1, book.AvailableCopies); 
                var loan = await context.Loans.FindAsync(loanId);
                Assert.NotNull(loan);
            }

            using (var context = GetDbContext(dbName))
            {
                var service = new LoanService(context);
                var existingLoan = await service.GetByIdAsync(loanId);
                
                var loanUpdate = new Loan {
                    Id = existingLoan!.Id,
                    BookId = existingLoan.BookId,
                    MemberId = existingLoan.MemberId,
                    LoanDate = existingLoan.LoanDate,
                    DueDate = existingLoan.DueDate,
                    ReturnDate = DateTime.Today
                };

                var result = await service.UpdateAsync(loanUpdate);
                Assert.True(result.Succeeded);
            }

            using (var context = GetDbContext(dbName))
            {
                var book = await context.Books.FirstAsync();
                Assert.Equal(2, book.AvailableCopies); 
            }

            using (var context = GetDbContext(dbName))
            {
                var service = new LoanService(context);
                var deleted = await service.DeleteAsync(loanId);
                Assert.True(deleted);
            }

            using (var context = GetDbContext(dbName))
            {
                Assert.Empty(context.Loans);
                var book = await context.Books.FirstAsync();
                Assert.Equal(2, book.AvailableCopies); 
            }
        }
    }
}
