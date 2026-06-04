using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers
{
    [Route("Scan")]
    public class UrlScannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UrlScannerService _scannerService;

        public UrlScannerController(
            ApplicationDbContext context,
            UrlScannerService scannerService)
        {
            _context = context;
            _scannerService = scannerService;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var scans = await _context.UrlScans
                .AsNoTracking()
                .OrderByDescending(x => x.ScannedAt)
                .Take(20)
                .ToListAsync();

            return View(scans);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Scan(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                TempData["Error"] = "Vui lòng nhập URL cần quét.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _scannerService.ScanAsync(url);

            _context.UrlScans.Add(result);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Quét URL thành công.";

            return RedirectToAction(nameof(Details), new { id = result.Id });
        }

        [HttpPost("ScanAjax")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScanAjax(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return Json(new { success = false, message = "Vui lòng nhập URL cần quét." });
            }

            try
            {
                var result = await _scannerService.ScanAsync(url);

                _context.UrlScans.Add(result);
                await _context.SaveChangesAsync();

                var reasonsList = string.IsNullOrWhiteSpace(result.Reasons)
                    ? new List<string>()
                    : result.Reasons
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = result.Id,
                        url = result.Url,
                        status = result.Status,
                        statusCode = result.StatusCode,
                        responseTimeMs = result.ResponseTimeMs,
                        isHttps = result.IsHttps,
                        riskScore = result.RiskScore,
                        riskLevel = result.RiskLevel,
                        reasons = reasonsList,
                        errorMessage = result.ErrorMessage,
                        scannedAt = result.ScannedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                        ipAddress = result.IpAddress,
                        countryName = result.CountryName,
                        countryCode = result.CountryCode,
                        city = result.City,
                        isp = result.Isp,
                        latitude = result.Latitude,
                        longitude = result.Longitude
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình quét: " + ex.Message });
            }
        }

        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound();
            }

            return View(scan);
        }

        [HttpGet("~/History")]
        public async Task<IActionResult> History(string search, string status, string riskLevel, int page = 1)
        {
            var query = _context.UrlScans.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x => x.Url.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(x => x.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(riskLevel))
            {
                query = query.Where(x => x.RiskLevel == riskLevel);
            }

            // Dữ liệu biểu đồ thống kê
            ViewBag.SafeCount = await query.CountAsync(x => x.RiskLevel == "Safe");
            ViewBag.WarningCount = await query.CountAsync(x => x.RiskLevel == "Warning");
            ViewBag.SuspiciousCount = await query.CountAsync(x => x.RiskLevel == "Suspicious");

            // Phân trang
            int pageSize = 10;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var scans = await query
                .OrderByDescending(x => x.ScannedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.RiskLevel = riskLevel;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(scans);
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound();
            }

            _context.UrlScans.Remove(scan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã xóa kết quả quét.";

            return RedirectToAction(nameof(Index));
        }
    }
}