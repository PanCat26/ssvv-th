using ssvv_th.Models;

namespace ssvv_th.Services
{
    public interface IReportService
    {
        Task<LoanReportViewModel> GenerateLoanReportAsync(DateTime? fromDate, DateTime? toDate, LoanReportType reportType, string? searchTerm);
    }
}
