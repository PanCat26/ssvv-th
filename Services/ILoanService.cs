using ssvv_th.Models;

namespace ssvv_th.Services
{
    public interface ILoanService
    {
        Task<List<Loan>> GetAllAsync();
        Task<Loan?> GetByIdAsync(int id);
        Task<Loan> CreateAsync(Loan loan);
        Task<Loan?> UpdateAsync(Loan loan);
        Task<bool> DeleteAsync(int id);
    }
}
