using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using System.Security.Claims;

namespace NetURLScanner.Controllers
{
    public class ScamReportController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ScamReportController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [AllowAnonymous]
        public IActionResult Create() => View();

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScamReport report, string? customBankName, List<IFormFile>? evidenceImages)
        {
            if (report.ReportType == "BankAccount")
            {
                if (report.BankId == "OTHER" && !string.IsNullOrWhiteSpace(customBankName))
                    report.BankId = customBankName.Trim();

                if (string.IsNullOrWhiteSpace(report.BankId) || string.IsNullOrWhiteSpace(report.BankAccountNumber))
                    ModelState.AddModelError("", "Vui lòng chọn Ngân hàng và nhập Số tài khoản.");
            }
            else if (string.IsNullOrWhiteSpace(report.UrlOrIp))
            {
                ModelState.AddModelError("UrlOrIp", "Vui lòng nhập URL hoặc IP.");
            }

            if (ModelState.IsValid)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    report.UserId = userId;

                report.Status = "Pending";
                report.CreatedAt = DateTime.Now;
                report.EvidenceImagePaths = await SaveEvidenceImagesAsync(evidenceImages);

                _context.ScamReports.Add(report);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Cảm ơn bạn! Báo cáo đã gửi và đang chờ duyệt.";
                return RedirectToAction(nameof(Create));
            }

            return View(report);
        }

        private async Task<string?> SaveEvidenceImagesAsync(List<IFormFile>? files)
        {
            if (files == null || files.Count == 0) return null;

            var dir = Path.Combine(_env.WebRootPath, "uploads", "scam-evidence");
            Directory.CreateDirectory(dir);

            var paths = new List<string>();
            foreach (var file in files.Take(5))
            {
                if (file.Length == 0 || file.Length > 5 * 1024 * 1024) continue;
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) continue;

                var name = $"ev_{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{ext}";
                var physical = Path.Combine(dir, name);
                await using var stream = new FileStream(physical, FileMode.Create);
                await file.CopyToAsync(stream);
                paths.Add($"/uploads/scam-evidence/{name}");
            }

            return paths.Count > 0 ? string.Join(",", paths) : null;
        }

        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Index()
        {
            var reports = await _context.ScamReports.Include(r => r.User).OrderByDescending(r => r.CreatedAt).ToListAsync();
            return View(reports);
        }

        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var report = await _context.ScamReports.Include(r => r.User).FirstOrDefaultAsync(m => m.Id == id);
            return report == null ? NotFound() : View(report);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var report = await _context.ScamReports.FindAsync(id);
            if (report == null) return NotFound();
            if (report.Status != "Pending")
            {
                TempData["Error"] = "Báo cáo này đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            report.Status = "Approved";

            if (report.ReportType == "Url" && !string.IsNullOrWhiteSpace(report.UrlOrIp))
            {
                var domain = ExtractDomain(report.UrlOrIp);
                if (!await _context.BlacklistedDomains.AnyAsync(b => b.Domain == domain))
                {
                    _context.BlacklistedDomains.Add(new BlacklistedDomain
                    {
                        Domain = domain,
                        Category = "Lừa đảo do người dùng báo cáo",
                        Reason = report.Evidence.Length > 450 ? report.Evidence[..450] + "..." : report.Evidence,
                        Severity = "High",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã duyệt báo cáo.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var report = await _context.ScamReports.FindAsync(id);
            if (report == null) return NotFound();
            if (report.Status != "Pending")
            {
                TempData["Error"] = "Báo cáo này đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            report.Status = "Rejected";
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối báo cáo.";
            return RedirectToAction(nameof(Index));
        }

        private static string ExtractDomain(string urlOrIp)
        {
            try
            {
                urlOrIp = urlOrIp.Trim();
                if (!urlOrIp.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !urlOrIp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    urlOrIp = "http://" + urlOrIp;
                return new Uri(urlOrIp).Host.ToLower();
            }
            catch
            {
                return urlOrIp;
            }
        }
    }
}
