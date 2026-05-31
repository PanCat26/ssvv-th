using Microsoft.AspNetCore.Mvc;
using ssvv_th.Models;
using ssvv_th.Services;

namespace ssvv_th.Controllers
{
    public class MemberController : Controller
    {
        private readonly IMemberService _memberService;

        public MemberController(IMemberService memberService)
        {
            _memberService = memberService;
        }

        // GET: /Member
        public async Task<IActionResult> Index()
        {
            List<Member> members = await _memberService.GetAllAsync();
            return View(members);
        }

        // GET: /Member/Create
        public IActionResult Create()
        {
            return View(new Member());
        }

        // POST: /Member/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Member member)
        {
            if (!ModelState.IsValid)
                return View(member);

            await _memberService.CreateAsync(member);
            TempData["Success"] = "Member created successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Member/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            Member? member = await _memberService.GetByIdAsync(id);
            if (member == null)
                return NotFound();

            return View(member);
        }

        // POST: /Member/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Member member)
        {
            if (id != member.Id)
                return BadRequest();

            if (!ModelState.IsValid)
                return View(member);

            Member? updated = await _memberService.UpdateAsync(member);
            if (updated == null)
                return NotFound();

            TempData["Success"] = "Member updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Member/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            Member? member = await _memberService.GetByIdAsync(id);
            if (member == null)
                return NotFound();

            return View(member);
        }

        // POST: /Member/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                bool deleted = await _memberService.DeleteAsync(id);
                if (!deleted)
                    return NotFound();
            }
            catch (InvalidOperationException exception)
            {
                TempData["Error"] = exception.Message;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = "Member deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
