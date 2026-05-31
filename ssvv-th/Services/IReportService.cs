using ssvv_th.Models;

namespace ssvv_th.Services
{
    public interface IReportService
    {
        Task<LoanReportViewModel> GenerateLoanReportAsync(LoanReportType reportType, string? searchTerm);
    }
}
