using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers
{
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

        public async Task<IActionResult> Index()
        {
            var scans = await _context.UrlScans
                .OrderByDescending(x => x.ScannedAt)
                .Take(20)
                .ToListAsync();

            return View(scans);
        }

        [HttpPost]
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

        public async Task<IActionResult> Details(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound();
            }

            return View(scan);
        }

        public async Task<IActionResult> History()
        {
            var scans = await _context.UrlScans
                .OrderByDescending(x => x.ScannedAt)
                .ToListAsync();

            return View(scans);
        }

        [HttpPost]
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