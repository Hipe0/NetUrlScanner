using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using Microsoft.AspNetCore.Authorization;

namespace NetURLScanner.Controllers
{
    [Route("Manager/BlacklistBank")]
    [Authorize(Roles = "Admin,Manager")]
    public class BlacklistedBankAccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlacklistedBankAccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.BlacklistedBankAccounts
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(accounts);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View(new BlacklistedBankAccount());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlacklistedBankAccount model)
        {
            if (ModelState.IsValid)
            {
                bool exists = await _context.BlacklistedBankAccounts
                    .AnyAsync(x => x.BankId == model.BankId && x.BankAccountNumber == model.BankAccountNumber);

                if (exists)
                {
                    ModelState.AddModelError("", "Số tài khoản này của ngân hàng đã tồn tại trong blacklist.");
                    return View(model);
                }

                model.BankId = model.BankId.Trim();
                model.BankAccountNumber = model.BankAccountNumber.Trim();
                model.BankAccountOwnerName = model.BankAccountOwnerName?.Trim();
                model.Reason = model.Reason.Trim();
                model.IsActive = true;
                model.CreatedAt = DateTime.Now;

                _context.BlacklistedBankAccounts.Add(model);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã thêm số tài khoản vào blacklist.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var account = await _context.BlacklistedBankAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            return View(account);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlacklistedBankAccount model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (ModelState.IsValid)
            {
                var accountInDb = await _context.BlacklistedBankAccounts.FindAsync(id);
                if (accountInDb == null)
                {
                    return NotFound();
                }

                bool duplicate = await _context.BlacklistedBankAccounts
                    .AnyAsync(x => x.Id != id && x.BankId == model.BankId && x.BankAccountNumber == model.BankAccountNumber);

                if (duplicate)
                {
                    ModelState.AddModelError("", "Số tài khoản này của ngân hàng đã tồn tại trong blacklist.");
                    return View(model);
                }

                accountInDb.BankId = model.BankId.Trim();
                accountInDb.BankAccountNumber = model.BankAccountNumber.Trim();
                accountInDb.BankAccountOwnerName = model.BankAccountOwnerName?.Trim();
                accountInDb.Reason = model.Reason.Trim();
                accountInDb.IsActive = model.IsActive;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Đã cập nhật thông tin blacklist ngân hàng.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var account = await _context.BlacklistedBankAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = account.IsActive
                ? "Đã kích hoạt chặn STK."
                : "Đã tạm tắt chặn STK.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var account = await _context.BlacklistedBankAccounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            _context.BlacklistedBankAccounts.Remove(account);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa STK khỏi blacklist.";
            return RedirectToAction(nameof(Index));
        }
    }
}
