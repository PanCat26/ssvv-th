using Microsoft.AspNetCore.Mvc;
using ssvv_th.Models;
using ssvv_th.Services;

namespace ssvv_th.Controllers
{
    public class BookController : Controller
    {
        private readonly IBookService _bookService;

        public BookController(IBookService bookService)
        {
            _bookService = bookService;
        }

        // GET: /Book
        public async Task<IActionResult> Index()
        {
            var books = await _bookService.GetAllAsync();
            return View(books);
        }

        // GET: /Book/Create
        public IActionResult Create()
        {
            return View(new Book());
        }

        // POST: /Book/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book book)
        {
            if (!ModelState.IsValid)
                return View(book);

            await _bookService.CreateAsync(book);
            TempData["Success"] = "Book created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Book/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var book = await _bookService.GetByIdAsync(id);
            if (book == null)
                return NotFound();

            return View(book);
        }

        // POST: /Book/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Book book)
        {
            if (id != book.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(book);

            var updated = await _bookService.UpdateAsync(book);
            if (updated == null)
                return NotFound();

            TempData["Success"] = "Book updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Book/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _bookService.GetByIdAsync(id);
            if (book == null)
                return NotFound();

            return View(book);
        }

        // POST: /Book/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var deleted = await _bookService.DeleteAsync(id);
            if (!deleted)
                return NotFound();

            TempData["Success"] = "Book deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
