using ssvv_th.Models;

namespace ssvv_th.Services
{
    public interface ILoanService
    {
        Task<List<Loan>> GetAllAsync();
        Task<Loan?> GetByIdAsync(int id);
        Task<LoanOperationResult> CreateAsync(Loan loan);
        Task<LoanOperationResult> UpdateAsync(Loan loan);
        Task<bool> DeleteAsync(int id);
    }
}
