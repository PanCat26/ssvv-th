using System.Text;
using Microsoft.AspNetCore.Mvc;
using ssvv_th.Models;
using ssvv_th.Services;

namespace ssvv_th.Controllers
{
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null, LoanReportType reportType = LoanReportType.All, string? searchTerm = null)
        {
            LoanReportViewModel report = await _reportService.GenerateLoanReportAsync(fromDate, toDate, reportType, searchTerm);
            return View(report);
        }

        public async Task<IActionResult> ExportCsv(DateTime? fromDate = null, DateTime? toDate = null, LoanReportType reportType = LoanReportType.All, string? searchTerm = null)
        {
            LoanReportViewModel report = await _reportService.GenerateLoanReportAsync(fromDate, toDate, reportType, searchTerm);
            StringBuilder csv = new StringBuilder();
            csv.AppendLine("Loan Id,Book,Author,Member,Email,Loan Date,Due Date,Return Date,Status");

            foreach (LoanReportItem item in report.Items)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    item.LoanId.ToString(),
                    EscapeCsv(item.BookTitle),
                    EscapeCsv(item.BookAuthor),
                    EscapeCsv(item.MemberName),
                    EscapeCsv(item.MemberEmail),
                    item.LoanDate.ToString("yyyy-MM-dd"),
                    item.DueDate.ToString("yyyy-MM-dd"),
                    item.ReturnDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                    item.Status
                }));
            }

            string fileName = $"loan-report-{reportType.ToString().ToLowerInvariant()}-{DateTime.Today:yyyyMMdd}.csv";
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
        }

        private static string EscapeCsv(string value)
        {
            if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
