using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers;

[Authorize(Roles = "Admin,Manager,User")]
[Route("BulkScan")]
public class BulkScanController : AppControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UrlScannerService _scannerService;

    public BulkScanController(ApplicationDbContext context, UrlScannerService scannerService)
    {
        _context = context;
        _scannerService = scannerService;
    }

    [HttpGet("")]
    public IActionResult Index() => View(new List<BulkScanResultItem>());

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? urlList, IFormFile? uploadFile)
    {
        var urls = ParseUrls(urlList);

        if (uploadFile != null && uploadFile.Length > 0)
            urls.AddRange(await UrlListFileParser.ParseAsync(uploadFile));

        urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();

        if (urls.Count == 0)
        {
            TempData["Error"] = "Vui lòng nhập URL hoặc upload file CSV / Excel / PDF chứa danh sách URL.";
            return View(new List<BulkScanResultItem>());
        }

        var userId = GetCurrentUserId();
        var results = new List<BulkScanResultItem>();
        var pendingScans = new List<NetURLScanner.Models.UrlScan>();

        foreach (var url in urls)
        {
            try
            {
                var scan = await _scannerService.ScanAsync(url);
                scan.UserId = userId;
                _context.UrlScans.Add(scan);
                pendingScans.Add(scan);
                results.Add(new BulkScanResultItem
                {
                    Url = scan.Url,
                    Status = scan.Status,
                    RiskLevel = scan.RiskLevel,
                    RiskScore = scan.RiskScore,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                results.Add(new BulkScanResultItem { Url = url, Success = false, ErrorMessage = ex.Message });
            }
        }

        if (pendingScans.Count > 0)
        {
            await _context.SaveChangesAsync();
            for (int i = 0, j = 0; i < results.Count; i++)
            {
                if (!results[i].Success) continue;
                results[i].ScanId = pendingScans[j++].Id;
            }
        }

        TempData["Success"] = $"Đã quét xong {results.Count(r => r.Success)}/{urls.Count} URL.";
        return View(results);
    }

    private static List<string> ParseUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
    }
}

public class BulkScanResultItem
{
    public string Url { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? RiskLevel { get; set; }
    public int RiskScore { get; set; }
    public int? ScanId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
