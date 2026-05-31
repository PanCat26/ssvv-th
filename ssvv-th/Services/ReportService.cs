using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;

namespace ssvv_th.Services
{
    public class ReportService : IReportService
    {
        private readonly LibraryDbContext _context;

        public ReportService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<LoanReportViewModel> GenerateLoanReportAsync(DateTime? fromDate, DateTime? toDate, LoanReportType reportType, string? searchTerm)
        {
            IQueryable<Loan> query = _context.Loans
                .Include(l => l.Book)
                .Include(l => l.Member);

            if (fromDate.HasValue)
            {
                query = query.Where(l => l.LoanDate.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                query = query.Where(l => l.LoanDate.Date <= toDate.Value.Date);
            }

            DateTime today = DateTime.Today;
            query = reportType switch
            {
                LoanReportType.Active => query.Where(l => l.ReturnDate == null && l.DueDate.Date >= today),
                LoanReportType.Overdue => query.Where(l => l.ReturnDate == null && l.DueDate.Date < today),
                LoanReportType.Returned => query.Where(l => l.ReturnDate != null),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                string normalizedSearchTerm = searchTerm.Trim();
                query = query.Where(l =>
                    (l.Book != null && (l.Book.Title.Contains(normalizedSearchTerm) || l.Book.Author.Contains(normalizedSearchTerm))) ||
                    (l.Member != null && (l.Member.Name.Contains(normalizedSearchTerm) || l.Member.Email.Contains(normalizedSearchTerm))));
            }

            List<Loan> loans = await query
                .OrderBy(l => l.DueDate)
                .ThenBy(l => l.Book!.Title)
                .ToListAsync();

            return new LoanReportViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                ReportType = reportType,
                SearchTerm = searchTerm,
                Items = loans.Select(l => new LoanReportItem
                {
                    LoanId = l.Id,
                    BookTitle = l.Book?.Title ?? string.Empty,
                    BookAuthor = l.Book?.Author ?? string.Empty,
                    MemberName = l.Member?.Name ?? string.Empty,
                    MemberEmail = l.Member?.Email ?? string.Empty,
                    LoanDate = l.LoanDate,
                    DueDate = l.DueDate,
                    ReturnDate = l.ReturnDate,
                    Status = l.Status
                }).ToList()
            };
        }
    }
}
