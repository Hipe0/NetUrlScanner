using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetURLScanner.Data;
using NetURLScanner.Services;
using System.Security.Claims;

namespace NetURLScanner.Controllers;

[Authorize(Roles = "Admin,Manager,User")]
[Route("OcrScan")]
public class OcrScanController : AppControllerBase
{
    private readonly OcrService _ocrService;
    private readonly UrlExtractionService _urlExtractor;
    private readonly UrlScannerService _scannerService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public OcrScanController(
        OcrService ocrService,
        UrlExtractionService urlExtractor,
        UrlScannerService scannerService,
        ApplicationDbContext context,
        IWebHostEnvironment env)
    {
        _ocrService = ocrService;
        _urlExtractor = urlExtractor;
        _scannerService = scannerService;
        _context = context;
        _env = env;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null && !user.IsPremium && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
            {
                TempData["Error"] = "Chức năng này chỉ dành cho tài khoản Premium. Vui lòng nâng cấp để sử dụng.";
                return RedirectToAction("Index", "Premium");
            }
        }
        return View(new OcrScanPageModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? imageFile)
    {
        var model = new OcrScanPageModel();

        var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userIdDb))
        {
            var user = await _context.Users.FindAsync(userIdDb);
            if (user != null && !user.IsPremium && !User.IsInRole("Admin") && !User.IsInRole("Manager"))
            {
                TempData["Error"] = "Chức năng này chỉ dành cho tài khoản Premium. Vui lòng nâng cấp để sử dụng.";
                return RedirectToAction("Index", "Premium");
            }
        }

        if (imageFile == null || imageFile.Length == 0)
        {
            TempData["Error"] = "Vui lòng chọn ảnh chụp màn hình (SMS, email, Zalo...).";
            return View(model);
        }

        try
        {
            var userId = GetCurrentUserId();
            model.SavedImagePath = await _ocrService.SaveUploadAsync(imageFile, userId);

            var physicalPath = Path.Combine(
                _env.WebRootPath,
                model.SavedImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            var ocr = _ocrService.ExtractText(physicalPath);
            model.ExtractedText = ocr.Text;
            model.OcrError = ocr.ErrorMessage;

            if (!ocr.Success)
            {
                TempData["Error"] = ocr.ErrorMessage ?? "OCR thất bại.";
                return View(model);
            }

            var urls = _urlExtractor.ExtractUrls(ocr.Text);
            model.ExtractedUrls = urls;

            if (urls.Count == 0)
            {
                TempData["Error"] = "Đã đọc được chữ nhưng không tìm thấy URL nào trong ảnh.";
                return View(model);
            }

            foreach (var url in urls)
            {
                try
                {
                    var scan = await _scannerService.ScanAsync(url);
                    scan.UserId = userId;
                    _context.UrlScans.Add(scan);
                    await _context.SaveChangesAsync();

                    model.ScanResults.Add(new OcrScanResultItem
                    {
                        Url = scan.Url,
                        Status = scan.Status,
                        RiskLevel = scan.RiskLevel,
                        RiskScore = scan.RiskScore,
                        ScanId = scan.Id,
                        Success = true
                    });
                }
                catch (Exception ex)
                {
                    model.ScanResults.Add(new OcrScanResultItem
                    {
                        Url = url,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            TempData["Success"] = $"Đã trích xuất {urls.Count} URL và quét xong.";
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View(model);
        }
    }
}

public class OcrScanPageModel
{
    public string? SavedImagePath { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string? OcrError { get; set; }
    public List<string> ExtractedUrls { get; set; } = new();
    public List<OcrScanResultItem> ScanResults { get; set; } = new();
}

public class OcrScanResultItem
{
    public string Url { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public int? ScanId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
