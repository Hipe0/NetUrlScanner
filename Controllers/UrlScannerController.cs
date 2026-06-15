using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;
using Microsoft.AspNetCore.Authorization;

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

        [HttpPost("/Scan/ScanAjax")]
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
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> Details(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound();
            }

            return View(scan);
        }

        [HttpGet("DetailsPartial/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> DetailsPartial(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound();
            }

            return PartialView("_ScanDetails", scan);
        }

        [HttpGet("~/History")]
        [Authorize(Roles = "Admin,Manager,User")]
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

        [HttpPost("/Scan/Delete/{id}")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> Delete(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return Json(new { success = false, message = "Không tìm thấy kết quả quét." });
            }

            _context.UrlScans.Remove(scan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa thành công" });
        }

        [HttpGet("/Scan/ExportPdf/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);
            if (scan == null)
            {
                return NotFound("Không tìm thấy kết quả quét.");
            }

            using var stream = new MemoryStream();
            using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            // Sử dụng font Arial của Windows với mã hóa Unicode (IDENTITY_H) để hỗ trợ tiếng Việt
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            iText.Kernel.Font.PdfFont pdfFont;
            
            try
            {
                pdfFont = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);
            }
            catch
            {
                // Fallback nếu không tìm thấy Arial
                pdfFont = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            }

            document.SetFont(pdfFont);

            document.Add(new iText.Layout.Element.Paragraph("NETURLSCANNER - SCAN REPORT")
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetFontSize(20));

            document.Add(new iText.Layout.Element.Paragraph("\n"));

            document.Add(new iText.Layout.Element.Paragraph($"URL: {scan.Url}").SetFontSize(14));
            document.Add(new iText.Layout.Element.Paragraph($"Risk Level: {scan.RiskLevel}"));
            document.Add(new iText.Layout.Element.Paragraph($"Risk Score: {scan.RiskScore}/100"));
            document.Add(new iText.Layout.Element.Paragraph($"Status: {scan.Status} (Code: {scan.StatusCode})"));
            document.Add(new iText.Layout.Element.Paragraph($"Response Time: {scan.ResponseTimeMs} ms"));
            document.Add(new iText.Layout.Element.Paragraph($"Scanned At: {scan.ScannedAt:dd/MM/yyyy HH:mm:ss}"));

            document.Add(new iText.Layout.Element.Paragraph("\n--- Server Information ---"));
            document.Add(new iText.Layout.Element.Paragraph($"IP Address: {(string.IsNullOrEmpty(scan.IpAddress) ? "N/A" : scan.IpAddress)}"));
            document.Add(new iText.Layout.Element.Paragraph($"Country: {scan.CountryName}"));
            document.Add(new iText.Layout.Element.Paragraph($"City: {scan.City}"));
            document.Add(new iText.Layout.Element.Paragraph($"ISP: {scan.Isp}"));

            if (!string.IsNullOrEmpty(scan.Reasons))
            {
                document.Add(new iText.Layout.Element.Paragraph("\n--- Risk Reasons ---"));
                foreach (var reason in scan.Reasons.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!string.IsNullOrWhiteSpace(reason))
                    {
                        document.Add(new iText.Layout.Element.Paragraph($"- {reason.Trim()}"));
                    }
                }
            }

            document.Close();

            return File(stream.ToArray(), "application/pdf", $"ScanReport_{scan.Id}.pdf");
        }
    }
}