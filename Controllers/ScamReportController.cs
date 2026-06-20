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

        public ScamReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ScamReport/Create
        [AllowAnonymous]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /ScamReport/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScamReport report, string? customBankName)
        {
            if (report.ReportType == "BankAccount")
            {
                if (report.BankId == "OTHER" && !string.IsNullOrWhiteSpace(customBankName))
                {
                    report.BankId = customBankName.Trim();
                }

                if (string.IsNullOrWhiteSpace(report.BankId) || string.IsNullOrWhiteSpace(report.BankAccountNumber))
                {
                    ModelState.AddModelError("", "Vui lòng chọn Ngân hàng và nhập Số tài khoản.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(report.UrlOrIp))
                {
                    ModelState.AddModelError("UrlOrIp", "Vui lòng nhập URL hoặc IP.");
                }
            }

            if (ModelState.IsValid)
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                    {
                        report.UserId = userId;
                    }
                }

                report.Status = "Pending";
                report.CreatedAt = DateTime.Now;

                _context.ScamReports.Add(report);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Cảm ơn bạn! Báo cáo của bạn đã được gửi và đang chờ duyệt.";
                return RedirectToAction(nameof(Create));
            }

            return View(report);
        }

        // GET: /ScamReport/Index
        // Chỉ dành cho Admin và Manager
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Index()
        {
            var reports = await _context.ScamReports
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return View(reports);
        }

        // GET: /ScamReport/Details/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var report = await _context.ScamReports
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (report == null)
            {
                return NotFound();
            }

            return View(report);
        }

        // POST: /ScamReport/Approve/5
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var report = await _context.ScamReports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }

            if (report.Status != "Pending")
            {
                TempData["Error"] = "Báo cáo này đã được xử lý.";
                return RedirectToAction(nameof(Index));
            }

            // Chuyển trạng thái
            report.Status = "Approved";

            // Thêm vào BlacklistedDomains nếu là URL
            if (report.ReportType == "Url" && !string.IsNullOrWhiteSpace(report.UrlOrIp))
            {
                var domain = ExtractDomain(report.UrlOrIp);
                
                // Kiểm tra xem đã có trong blacklist chưa
                bool exists = await _context.BlacklistedDomains.AnyAsync(b => b.Domain == domain);
                if (!exists)
                {
                    var blacklistDomain = new BlacklistedDomain
                    {
                        Domain = domain,
                        Category = "Lừa đảo do người dùng báo cáo",
                        Reason = report.Evidence.Length > 450 ? report.Evidence.Substring(0, 450) + "..." : report.Evidence,
                        Severity = "High",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.BlacklistedDomains.Add(blacklistDomain);
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã duyệt báo cáo và thêm vào danh sách đen thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /ScamReport/Reject/5
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var report = await _context.ScamReports.FindAsync(id);
            if (report == null)
            {
                return NotFound();
            }

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

        private string ExtractDomain(string urlOrIp)
        {
            try
            {
                urlOrIp = urlOrIp.Trim();
                if (!urlOrIp.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                    !urlOrIp.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    urlOrIp = "http://" + urlOrIp;
                }

                var uri = new Uri(urlOrIp);
                return uri.Host.ToLower();
            }
            catch
            {
                // Nếu lỗi parsing, trả về chuỗi gốc (có thể là IP hoặc chuỗi không hợp lệ)
                return urlOrIp;
            }
        }
    }
}
