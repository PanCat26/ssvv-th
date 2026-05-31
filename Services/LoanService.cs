using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ssvv_th.Data;
using ssvv_th.Models;

namespace ssvv_th.Services
{
    public class LoanService : ILoanService
    {
        private readonly LibraryDbContext _context;

        public LoanService(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<List<Loan>> GetAllAsync()
        {
            return await _context.Loans
                .Include(l => l.Book)
                .Include(l => l.Member)
                .OrderByDescending(l => l.LoanDate)
                .ToListAsync();
        }

        public async Task<Loan?> GetByIdAsync(int id)
        {
            return await _context.Loans
                .Include(l => l.Book)
                .Include(l => l.Member)
                .FirstOrDefaultAsync(l => l.Id == id);
        }

        public async Task<LoanOperationResult> CreateAsync(Loan loan)
        {
            List<LoanValidationError> errors = await ValidateAsync(loan);
            if (errors.Count > 0)
                return LoanOperationResult.Failure(errors);

            Book book = await _context.Books.FirstAsync(b => b.Id == loan.BookId);
            Member member = await _context.Members.FirstAsync(m => m.Id == loan.MemberId);

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            if (loan.ReturnDate == null)
                book.AvailableCopies--;

            _context.Loans.Add(loan);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            loan.Book = book;
            loan.Member = member;

            return LoanOperationResult.Success(loan);
        }

        public async Task<LoanOperationResult> UpdateAsync(Loan loan)
        {
            Loan? existing = await _context.Loans.FirstOrDefaultAsync(l => l.Id == loan.Id);
            if (existing == null)
                return LoanOperationResult.Failure(new LoanValidationError(string.Empty, "The loan could not be found."));

            List<LoanValidationError> errors = await ValidateAsync(loan, existing);
            if (errors.Count > 0)
                return LoanOperationResult.Failure(errors);

            int[] bookIds = new[] { existing.BookId, loan.BookId }.Distinct().ToArray();
            Dictionary<int, Book> books = await _context.Books
                .Where(b => bookIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);
            Member member = await _context.Members.FirstAsync(m => m.Id == loan.MemberId);

            Book previousBook = books[existing.BookId];
            Book currentBook = books[loan.BookId];

            await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

            ApplyInventoryChanges(existing, loan, previousBook, currentBook);

            existing.BookId = loan.BookId;
            existing.MemberId = loan.MemberId;
            existing.LoanDate = loan.LoanDate;
            existing.DueDate = loan.DueDate;
            existing.ReturnDate = loan.ReturnDate;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            existing.Book = currentBook;
            existing.Member = member;

            return LoanOperationResult.Success(existing);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            Loan? loan = await _context.Loans.FirstOrDefaultAsync(l => l.Id == id);
            if (loan == null)
                return false;

            if (loan.ReturnDate == null)
            {
                Book book = await _context.Books.FirstAsync(b => b.Id == loan.BookId);
                await using IDbContextTransaction transaction = await _context.Database.BeginTransactionAsync();

                book.AvailableCopies++;
                _context.Loans.Remove(loan);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }

            _context.Loans.Remove(loan);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<List<LoanValidationError>> ValidateAsync(Loan loan, Loan? existingLoan = null)
        {
            List<LoanValidationError> errors = new List<LoanValidationError>();

            if (loan.DueDate.Date < loan.LoanDate.Date)
            {
                errors.Add(new LoanValidationError(nameof(Loan.DueDate), "Due date cannot be before the loan date."));
            }

            if (loan.ReturnDate.HasValue && loan.ReturnDate.Value.Date < loan.LoanDate.Date)
            {
                errors.Add(new LoanValidationError(nameof(Loan.ReturnDate), "Return date cannot be before the loan date."));
            }

            int[] bookIds = existingLoan == null
                ? new[] { loan.BookId }
                : new[] { loan.BookId, existingLoan.BookId }.Distinct().ToArray();

            Dictionary<int, Book> books = await _context.Books
                .Where(b => bookIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id);

            if (!books.ContainsKey(loan.BookId))
            {
                errors.Add(new LoanValidationError(nameof(Loan.BookId), "The selected book does not exist."));
            }

            bool memberExists = await _context.Members.AnyAsync(m => m.Id == loan.MemberId);
            if (!memberExists)
            {
                errors.Add(new LoanValidationError(nameof(Loan.MemberId), "The selected member does not exist."));
            }

            if (errors.Count > 0)
                return errors;

            Book targetBook = books[loan.BookId];
            bool wasActive = existingLoan?.ReturnDate == null;
            bool willBeActive = loan.ReturnDate == null;
            bool isChangingBook = existingLoan != null && existingLoan.BookId != loan.BookId;
            bool isReopeningLoan = existingLoan != null && existingLoan.ReturnDate != null && willBeActive;

            if (existingLoan == null)
            {
                if (willBeActive && targetBook.AvailableCopies <= 0)
                {
                    errors.Add(new LoanValidationError(nameof(Loan.BookId), "This book is currently unavailable."));
                }

                return errors;
            }

            if (isChangingBook && willBeActive && targetBook.AvailableCopies <= 0)
            {
                errors.Add(new LoanValidationError(nameof(Loan.BookId), "This book is currently unavailable."));
            }

            if (!isChangingBook && isReopeningLoan && targetBook.AvailableCopies <= 0)
            {
                errors.Add(new LoanValidationError(nameof(Loan.ReturnDate), "The loan cannot be reopened because the book has no available copies."));
            }

            if (wasActive && !willBeActive && loan.ReturnDate == null)
            {
                errors.Add(new LoanValidationError(nameof(Loan.ReturnDate), "Return date is required when closing a loan."));
            }

            return errors;
        }

        private static void ApplyInventoryChanges(Loan existingLoan, Loan updatedLoan, Book previousBook, Book currentBook)
        {
            bool wasActive = existingLoan.ReturnDate == null;
            bool willBeActive = updatedLoan.ReturnDate == null;
            bool isChangingBook = existingLoan.BookId != updatedLoan.BookId;

            if (wasActive)
            {
                previousBook.AvailableCopies++;
            }

            if (willBeActive)
            {
                currentBook.AvailableCopies--;
            }

            if (!wasActive && !willBeActive && isChangingBook)
            {
                return;
            }
        }
    }
}
