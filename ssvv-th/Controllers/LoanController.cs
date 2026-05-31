using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ssvv_th.Models;
using ssvv_th.Services;

namespace ssvv_th.Controllers
{
    public class LoanController : Controller
    {
        private readonly ILoanService _loanService;
        private readonly IBookService _bookService;
        private readonly IMemberService _memberService;

        public LoanController(
            ILoanService loanService,
            IBookService bookService,
            IMemberService memberService)
        {
            _loanService = loanService;
            _bookService = bookService;
            _memberService = memberService;
        }

        // GET: /Loan
        public async Task<IActionResult> Index()
        {
            List<Loan> loans = await _loanService.GetAllAsync();
            return View(loans);
        }

        // GET: /Loan/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View(new Loan());
        }

        // POST: /Loan/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Loan loan)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            LoanOperationResult result = await _loanService.CreateAsync(loan);
            if (!result.Succeeded)
            {
                AddServiceErrors(result);
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            TempData["Success"] = "Loan created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loan/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            Loan? loan = await _loanService.GetByIdAsync(id);
            if (loan == null)
                return NotFound();

            await PopulateDropdownsAsync(loan);
            return View(loan);
        }

        // POST: /Loan/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Loan loan)
        {
            if (id != loan.Id)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            LoanOperationResult result = await _loanService.UpdateAsync(loan);
            if (!result.Succeeded)
            {
                AddServiceErrors(result);
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            if (result.Loan == null)
                return NotFound();

            TempData["Success"] = "Loan updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loan/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            Loan? loan = await _loanService.GetByIdAsync(id);
            if (loan == null)
                return NotFound();

            return View(loan);
        }

        // POST: /Loan/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            bool deleted = await _loanService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            TempData["Success"] = "Loan deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private void AddServiceErrors(LoanOperationResult result)
        {
            foreach (LoanValidationError error in result.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }

        private async Task PopulateDropdownsAsync(Loan? loan = null)
        {
            List<Book> books = await _bookService.GetAllAsync();
            List<Member> members = await _memberService.GetAllAsync();
            IEnumerable<object> borrowableBooks = books
                .Where(b => b.AvailableCopies > 0 || b.Id == loan?.BookId)
                .Select(b => new
                {
                    b.Id,
                    Display = $"{b.Title} ({b.Author}) - {b.AvailableCopies} available"
                });

            ViewBag.Books = new SelectList(
                borrowableBooks,
                "Id", "Display", loan?.BookId);

            ViewBag.Members = new SelectList(
                members.Select(m => new { m.Id, Display = $"{m.Name} ({m.Email})" }),
                "Id", "Display", loan?.MemberId);
        }
    }
}
