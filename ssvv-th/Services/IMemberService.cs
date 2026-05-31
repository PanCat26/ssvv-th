using ssvv_th.Models;

namespace ssvv_th.Services
{
    public interface IMemberService
    {
        Task<List<Member>> GetAllAsync();
        Task<Member?> GetByIdAsync(int id);
        Task<Member> CreateAsync(Member member);
        Task<Member?> UpdateAsync(Member member);
        Task<bool> DeleteAsync(int id);
    }
}
