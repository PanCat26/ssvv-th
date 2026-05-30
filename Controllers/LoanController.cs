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
            var loans = await _loanService.GetAllAsync();
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
            ValidateDates(loan);

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            await _loanService.CreateAsync(loan);
            TempData["Success"] = "Loan created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loan/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var loan = await _loanService.GetByIdAsync(id);
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

            ValidateDates(loan);

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(loan);
                return View(loan);
            }

            var updated = await _loanService.UpdateAsync(loan);
            if (updated == null)
                return NotFound();

            TempData["Success"] = "Loan updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Loan/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var loan = await _loanService.GetByIdAsync(id);
            if (loan == null)
                return NotFound();

            return View(loan);
        }

        // POST: /Loan/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var deleted = await _loanService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            TempData["Success"] = "Loan deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private void ValidateDates(Loan loan)
        {
            if (loan.DueDate.Date < loan.LoanDate.Date)
                ModelState.AddModelError(nameof(Loan.DueDate), "Due date cannot be before the loan date.");

            if (loan.ReturnDate.HasValue && loan.ReturnDate.Value.Date < loan.LoanDate.Date)
                ModelState.AddModelError(nameof(Loan.ReturnDate), "Return date cannot be before the loan date.");
        }

        private async Task PopulateDropdownsAsync(Loan? loan = null)
        {
            var books = await _bookService.GetAllAsync();
            var members = await _memberService.GetAllAsync();

            ViewBag.Books = new SelectList(
                books.Select(b => new { b.Id, Display = $"{b.Title} ({b.Author})" }),
                "Id", "Display", loan?.BookId);

            ViewBag.Members = new SelectList(
                members.Select(m => new { m.Id, Display = $"{m.Name} ({m.Email})" }),
                "Id", "Display", loan?.MemberId);
        }
    }
}
