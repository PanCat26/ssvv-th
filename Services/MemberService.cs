using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;

namespace ssvv_th.Services
{
    public class MemberService : IMemberService
    {
        private readonly LibraryDbContext _context;

        public MemberService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<List<Member>> GetAllAsync()
        {
            return await _context.Members
                .OrderBy(m => m.Name)
                .ToListAsync();
        }

        public async Task<Member?> GetByIdAsync(int id)
        {
            return await _context.Members.FindAsync(id);
        }

        public async Task<Member> CreateAsync(Member member)
        {
            _context.Members.Add(member);
            await _context.SaveChangesAsync();
            return member;
        }

        public async Task<Member?> UpdateAsync(Member member)
        {
            Member? existing = await _context.Members.FindAsync(member.Id);
            if (existing == null)
                return null;

            existing.Name = member.Name;
            existing.Email = member.Email;
            existing.Phone = member.Phone;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            Member? member = await _context.Members.FindAsync(id);
            if (member == null)
                return false;

            bool hasRelatedLoans = await _context.Loans.AnyAsync(loan => loan.MemberId == id);
            if (hasRelatedLoans)
            {
                throw new InvalidOperationException("This member cannot be deleted because they are referenced by one or more loans.");
            }

            _context.Members.Remove(member);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
