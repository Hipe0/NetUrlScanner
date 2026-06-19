using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;

namespace NetURLScanner.Controllers;

[Authorize]
[Route("Dashboard")]
public class DashboardController : AppControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var scanQuery = _context.UrlScans.AsNoTracking().AsQueryable();
        if (!isAdmin)
            scanQuery = scanQuery.Where(x => x.UserId == userId);

        var today = DateTime.Today;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);

        ViewBag.TotalScans = await scanQuery.CountAsync();
        ViewBag.TodayScans = await scanQuery.CountAsync(x => x.ScannedAt >= today);
        ViewBag.WeekScans = await scanQuery.CountAsync(x => x.ScannedAt >= weekStart);
        ViewBag.SafeCount = await scanQuery.CountAsync(x => x.RiskLevel == "Safe");
        ViewBag.WarningCount = await scanQuery.CountAsync(x => x.RiskLevel == "Warning");
        ViewBag.SuspiciousCount = await scanQuery.CountAsync(x => x.RiskLevel == "Suspicious");
        ViewBag.PendingFeedbacks = isAdmin
            ? await _context.SiteFeedbacks.CountAsync(x => x.Status == "Pending")
            : 0;
        ViewBag.PendingScamReports = User.IsInRole("Admin") || User.IsInRole("Manager")
            ? await _context.ScamReports.CountAsync(x => x.Status == "Pending")
            : 0;

        ViewBag.TopDomains = await scanQuery
            .GroupBy(x => x.Url)
            .Select(g => new { Url = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentSuspicious = await scanQuery
            .Where(x => x.RiskLevel == "Suspicious")
            .OrderByDescending(x => x.ScannedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.DailyLabels = new List<string>();
        ViewBag.DailyCounts = new List<int>();
        for (int i = 6; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            ViewBag.DailyLabels.Add(day.ToString("dd/MM"));
            ViewBag.DailyCounts.Add(await scanQuery.CountAsync(x => x.ScannedAt >= day && x.ScannedAt < day.AddDays(1)));
        }

        ViewBag.IsAdmin = isAdmin;
        return View();
    }
}
