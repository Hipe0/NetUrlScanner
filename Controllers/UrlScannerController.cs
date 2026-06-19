using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;
using Microsoft.AspNetCore.Authorization;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers
{
    [Route("Scan")]
    public class UrlScannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UrlScannerService _scannerService;
        private readonly DomainVoteService _domainVoteService;

        public UrlScannerController(
            ApplicationDbContext context,
            UrlScannerService scannerService,
            DomainVoteService domainVoteService)
        {
            _context = context;
            _scannerService = scannerService;
            _domainVoteService = domainVoteService;
        }

        /// <summary>
        /// Trang chủ giao diện quét URL: hiển thị 20 kết quả quét gần đây nhất.
        /// </summary>
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            List<UrlScan> scans = new List<UrlScan>();
            var currentUserId = GetCurrentUserId();
            if (currentUserId != null)
            {
                scans = await _context.UrlScans
                    .Where(x => x.UserId == currentUserId)
                    .AsNoTracking()
                    .OrderByDescending(x => x.ScannedAt)
                    .Take(20)
                    .ToListAsync();
            }

            return View(scans);
        }

        /// <summary>
        /// API xử lý quét URL gửi từ Client qua Ajax, ghi nhận kết quả vào cơ sở dữ liệu.
        /// </summary>
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

                var currentUserId = GetCurrentUserId();
                if (currentUserId != null)
                {
                    result.UserId = currentUserId;
                    _context.UrlScans.Add(result);
                    await _context.SaveChangesAsync();
                }

                // Chuyển đổi chuỗi lý do phân tách bằng dấu chấm phẩy sang danh sách mảng gọn gàng để trả về Client.
                var reasonsList = string.IsNullOrWhiteSpace(result.Reasons)
                    ? []
                    : result.Reasons
                        .Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                var domainStats = await _domainVoteService.GetStatsAsync(
                    result.NormalizedDomain ?? result.Url,
                    GetCurrentUserId());

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        id = result.Id,
                        url = result.Url,
                        normalizedDomain = domainStats.NormalizedDomain,
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
                        longitude = result.Longitude,
                        siteCategory = result.SiteCategory,
                        siteCategoryTags = result.SiteCategoryTags,
                        safeBrowsingStatus = result.SafeBrowsingStatus,
                        safeBrowsingThreatType = result.SafeBrowsingThreatType,
                        domainUpVotes = domainStats.UpVotes,
                        domainDownVotes = domainStats.DownVotes,
                        domainNetScore = domainStats.NetScore,
                        userDomainVote = domainStats.CurrentUserVote
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình quét: " + ex.Message });
            }
        }

        /// <summary>
        /// Trang chi tiết đầy đủ của một lượt quét.
        /// </summary>
        [HttpGet("Details/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> Details(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);
            if (scan == null)
            {
                return NotFound();
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && scan.UserId != currentUserId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            return View(scan);
        }

        /// <summary>
        /// Lấy phần giao diện Partial View thông tin chi tiết lượt quét phục vụ hiển thị Modal trên frontend.
        /// </summary>
        [HttpGet("DetailsPartial/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> DetailsPartial(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);
            if (scan == null)
            {
                return NotFound();
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && scan.UserId != currentUserId)
            {
                return Forbid();
            }

            ViewBag.DomainVoteStats = await _domainVoteService.GetStatsAsync(
                scan.NormalizedDomain ?? scan.Url,
                currentUserId);

            return PartialView("_ScanDetails", scan);
        }

        /// <summary>
        /// Trang lịch sử tìm kiếm, lọc trạng thái, lọc mức độ rủi ro và thống kê số lượng tổng quan kèm phân trang.
        /// </summary>
        [HttpGet("~/History")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> History(string search, string status, string riskLevel, int page = 1)
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");

            var query = _context.UrlScans.AsNoTracking().AsQueryable();

            if (!isAdmin)
            {
                query = query.Where(x => x.UserId == currentUserId);
            }

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

            // Lấy dữ liệu đếm cho biểu đồ thống kê
            ViewBag.SafeCount = await query.CountAsync(x => x.RiskLevel == "Safe");
            ViewBag.WarningCount = await query.CountAsync(x => x.RiskLevel == "Warning");
            ViewBag.SuspiciousCount = await query.CountAsync(x => x.RiskLevel == "Suspicious");

            // Xử lý phân trang đầu ra
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

        /// <summary>
        /// Xóa một lượt quét đã lưu trong hệ thống lịch sử.
        /// </summary>
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

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && scan.UserId != currentUserId)
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa kết quả quét này." });
            }

            _context.UrlScans.Remove(scan);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã xóa thành công" });
        }

        /// <summary>
        /// Kết xuất báo cáo kết quả quét dưới dạng tệp tin PDF hỗ trợ Unicode tiếng Việt đầy đủ.
        /// </summary>
        [HttpGet("/Scan/ExportPdf/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> ExportPdf(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);
            if (scan == null)
            {
                return NotFound("Không tìm thấy kết quả quét.");
            }

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && scan.UserId != currentUserId)
            {
                return RedirectToAction("AccessDenied", "Account");
            }

            using var stream = new MemoryStream();
            using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            // Sử dụng font Arial cài đặt sẵn trên Windows để hỗ trợ hiển thị tiếng Việt Unicode
            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
            iText.Kernel.Font.PdfFont pdfFont;
            
            try
            {
                pdfFont = iText.Kernel.Font.PdfFontFactory.CreateFont(fontPath, iText.IO.Font.PdfEncodings.IDENTITY_H);
            }
            catch
            {
                // Dự phòng fallback nếu không tìm thấy tệp font Arial trên máy chủ
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

        [HttpGet("/Scan/ExportCsv")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> ExportCsv(string? search, string? status, string? riskLevel)
        {
            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            var query = _context.UrlScans.AsNoTracking().AsQueryable();

            if (!isAdmin)
                query = query.Where(x => x.UserId == currentUserId);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => x.Url.Contains(search));
            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(riskLevel))
                query = query.Where(x => x.RiskLevel == riskLevel);

            var scans = await query.OrderByDescending(x => x.ScannedAt).Take(1000).ToListAsync();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Id,Url,Domain,Status,StatusCode,ResponseTimeMs,RiskLevel,RiskScore,Category,SafeBrowsing,Label,Notes,ScannedAt");
            foreach (var s in scans)
            {
                sb.AppendLine($"{s.Id},\"{s.Url.Replace("\"", "\"\"")}\",{s.NormalizedDomain},{s.Status},{s.StatusCode},{s.ResponseTimeMs},{s.RiskLevel},{s.RiskScore},\"{s.SiteCategory}\",{s.SafeBrowsingStatus},\"{s.UserLabel}\",\"{s.UserNotes?.Replace("\"", "\"\"")}\",{s.ScannedAt:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", $"ScanHistory_{DateTime.Now:yyyyMMdd_HHmm}.csv");
        }

        [HttpPost("/Scan/UpdateNote/{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNote(int id, string? userLabel, string? userNotes)
        {
            var scan = await _context.UrlScans.FindAsync(id);
            if (scan == null)
                return Json(new { success = false, message = "Không tìm thấy kết quả quét." });

            var currentUserId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin");
            if (!isAdmin && scan.UserId != currentUserId)
                return Json(new { success = false, message = "Bạn không có quyền sửa ghi chú này." });

            scan.UserLabel = string.IsNullOrWhiteSpace(userLabel) ? null : userLabel.Trim()[..Math.Min(50, userLabel.Trim().Length)];
            scan.UserNotes = string.IsNullOrWhiteSpace(userNotes) ? null : userNotes.Trim()[..Math.Min(500, userNotes.Trim().Length)];
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đã lưu nhãn và ghi chú." });
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }
            return null;
        }
    }
}