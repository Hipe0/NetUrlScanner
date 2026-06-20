using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using System.Security.Claims;

namespace NetURLScanner.Controllers
{
    [Authorize]
    public class BankAccountCheckerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BankAccountCheckerController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null && !user.IsPremium && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
                {
                    TempData["Error"] = "Chức năng này chỉ dành cho tài khoản Premium. Vui lòng nâng cấp để sử dụng.";
                    return RedirectToAction("Index", "Premium");
                }
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string bankId, string bankAccountNumber, string? customBankName)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null && !user.IsPremium && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
                {
                    TempData["Error"] = "Chức năng này chỉ dành cho tài khoản Premium. Vui lòng nâng cấp để sử dụng.";
                    return RedirectToAction("Index", "Premium");
                }
            }

            if (bankId == "OTHER" && !string.IsNullOrWhiteSpace(customBankName))
            {
                bankId = customBankName.Trim();
            }

            ViewBag.BankId = bankId;
            ViewBag.BankAccountNumber = bankAccountNumber;

            if (string.IsNullOrWhiteSpace(bankId) || string.IsNullOrWhiteSpace(bankAccountNumber))
            {
                ViewBag.Error = "Vui lòng chọn Ngân hàng và nhập Số tài khoản.";
                return View();
            }

            // Perform check in ScamReports
            var reports = await _context.ScamReports
                .AsNoTracking()
                .Where(r => r.ReportType == "BankAccount"
                         && r.BankId == bankId
                         && r.BankAccountNumber == bankAccountNumber
                         && r.Status == "Approved")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.HasSearched = true;
            return View(reports);
        }
    }
}
