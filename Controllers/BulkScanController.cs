using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
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
    public IActionResult Index()
    {
        return View(new List<BulkScanResultItem>());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(string? urlList, IFormFile? csvFile)
    {
        var urls = ParseUrls(urlList);

        if (csvFile != null && csvFile.Length > 0)
        {
            using var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.UTF8);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var firstCol = line.Split(',')[0].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(firstCol) && !firstCol.Equals("url", StringComparison.OrdinalIgnoreCase))
                    urls.Add(firstCol);
            }
        }

        urls = urls.Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToList();

        if (urls.Count == 0)
        {
            TempData["Error"] = "Vui lòng nhập ít nhất một URL hoặc upload file CSV.";
            return View(new List<BulkScanResultItem>());
        }

        var userId = GetCurrentUserId();
        var results = new List<BulkScanResultItem>();

        foreach (var url in urls)
        {
            try
            {
                var scan = await _scannerService.ScanAsync(url);
                scan.UserId = userId;
                _context.UrlScans.Add(scan);
                await _context.SaveChangesAsync();

                results.Add(new BulkScanResultItem
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
                results.Add(new BulkScanResultItem
                {
                    Url = url,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        TempData["Success"] = $"Đã quét xong {results.Count(r => r.Success)}/{urls.Count} URL.";
        return View(results);
    }

    private static List<string> ParseUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return raw
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
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
