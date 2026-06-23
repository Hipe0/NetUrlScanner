using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetURLScanner.Data;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers;

/// <summary>Quét mã QR chứa URL — decode ảnh hoặc camera, quét rủi ro trên cùng trang.</summary>
[Authorize(Roles = "Admin,Manager,User")]
[Route("QrScan")]
public class QrScanController : AppControllerBase
{
    private readonly QrScanService _qrScanService;
    private readonly UrlExtractionService _urlExtractor;
    private readonly UrlScannerService _scannerService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public QrScanController(
        QrScanService qrScanService,
        UrlExtractionService urlExtractor,
        UrlScannerService scannerService,
        ApplicationDbContext context,
        IWebHostEnvironment env)
    {
        _qrScanService = qrScanService;
        _urlExtractor = urlExtractor;
        _scannerService = scannerService;
        _context = context;
        _env = env;
    }

    [HttpGet("")]
    public IActionResult Index() => View(new QrScanPageModel());

    /// <summary>POST: upload ảnh hoặc <paramref name="qrTextFromCamera"/> từ camera → decode → quét URL → hiển thị kết quả.</summary>
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(IFormFile? qrImage, string? qrTextFromCamera)
    {
        var model = new QrScanPageModel();
        string? qrText = null;

        if (qrImage != null && qrImage.Length > 0)
        {
            try
            {
                var userId = GetCurrentUserId();
                model.SavedImagePath = await _qrScanService.SaveUploadAsync(qrImage, userId);

                var physicalPath = Path.Combine(
                    _env.WebRootPath,
                    model.SavedImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

                var decode = _qrScanService.DecodeFromFile(physicalPath);
                if (!decode.Success)
                {
                    TempData["Error"] = decode.ErrorMessage ?? "Không đọc được mã QR.";
                    return View(model);
                }

                qrText = decode.Text;
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return View(model);
            }
        }
        else if (!string.IsNullOrWhiteSpace(qrTextFromCamera))
        {
            qrText = qrTextFromCamera.Trim();
            model.FromCamera = true;
        }
        else
        {
            TempData["Error"] = "Vui lòng chọn ảnh QR hoặc quét bằng camera.";
            return View(model);
        }

        model.QrText = qrText ?? string.Empty;

        var url = ResolveScanUrl(qrText!);
        if (url == null)
        {
            TempData["Error"] = "Mã QR không chứa URL hợp lệ.";
            return View(model);
        }

        model.ExtractedUrl = url;

        try
        {
            var scan = await _scannerService.ScanAsync(url);
            scan.UserId = GetCurrentUserId();
            _context.UrlScans.Add(scan);
            await _context.SaveChangesAsync();

            model.ScanResult = new OcrScanResultItem
            {
                Url = scan.Url,
                Status = scan.Status,
                RiskLevel = scan.RiskLevel,
                RiskScore = scan.RiskScore,
                ScanId = scan.Id,
                Success = true
            };

            TempData["Success"] = "Đã đọc mã QR và quét URL xong.";
        }
        catch (Exception ex)
        {
            model.ScanResult = new OcrScanResultItem
            {
                Url = url,
                Success = false,
                ErrorMessage = ex.Message
            };
            TempData["Error"] = "Đọc được QR nhưng quét URL thất bại.";
        }

        return View(model);
    }

    private string? ResolveScanUrl(string qrText)
    {
        var urls = _urlExtractor.ExtractUrls(qrText);
        if (urls.Count > 0)
            return urls[0];

        var trimmed = qrText.Trim();
        if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('.'))
            trimmed = "https://" + trimmed;

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return uri.ToString();
        }

        return null;
    }
}

public class QrScanPageModel
{
    public string? SavedImagePath { get; set; }
    public bool FromCamera { get; set; }
    public string QrText { get; set; } = string.Empty;
    public string? ExtractedUrl { get; set; }
    public OcrScanResultItem? ScanResult { get; set; }
}
