using Microsoft.EntityFrameworkCore;
using ssvv_th.Data;
using ssvv_th.Models;

namespace ssvv_th.Services
{
    public class BookService : IBookService
    {
        private readonly LibraryDbContext _context;

        public BookService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<List<Book>> GetAllAsync()
        {
            return await _context.Books
                .OrderBy(b => b.Title)
                .ToListAsync();
        }

        public async Task<Book?> GetByIdAsync(int id)
        {
            return await _context.Books.FindAsync(id);
        }

        public async Task<Book> CreateAsync(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            return book;
        }

        public async Task<Book?> UpdateAsync(Book book)
        {
            Book? existing = await _context.Books.FindAsync(book.Id);
            if (existing == null)
                return null;

            existing.Title = book.Title;
            existing.Author = book.Author;
            existing.ISBN = book.ISBN;
            existing.AvailableCopies = book.AvailableCopies;

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            Book? book = await _context.Books.FindAsync(id);
            if (book == null)
                return false;

            bool hasRelatedLoans = await _context.Loans.AnyAsync(loan => loan.BookId == id);
            if (hasRelatedLoans)
            {
                throw new InvalidOperationException("This book cannot be deleted because it is referenced by one or more loans.");
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
