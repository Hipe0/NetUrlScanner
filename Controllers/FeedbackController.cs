using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers;

public class FeedbackController : AppControllerBase
{
    private readonly ApplicationDbContext _context;

    public FeedbackController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Route("Feedback")]
    public IActionResult Create()
    {
        var model = new SiteFeedback();
        if (User.Identity?.IsAuthenticated == true)
        {
            model.SenderEmail = GetCurrentUserEmail();
            model.SenderName = User.Identity.Name ?? model.SenderEmail;
        }
        return View(model);
    }

    [HttpPost]
    [Route("Feedback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SiteFeedback model)
    {
        if (!ModelState.IsValid)
            return View(model);

        model.UserId = GetCurrentUserId();
        model.Status = "Pending";
        model.CreatedAt = DateTime.Now;

        _context.SiteFeedbacks.Add(model);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Cảm ơn bạn! Góp ý đã được gửi tới quản trị viên.";
        return RedirectToAction(nameof(Create));
    }

    [Authorize(Roles = "Admin")]
    [HttpGet]
    [Route("Admin/Feedback")]
    public async Task<IActionResult> Index(string? status)
    {
        var query = _context.SiteFeedbacks.AsNoTracking().Include(x => x.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        ViewBag.Status = status;
        return View(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    [Route("Admin/Feedback/Resolve/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string adminReply)
    {
        var item = await _context.SiteFeedbacks.FindAsync(id);
        if (item == null) return NotFound();

        item.Status = "Resolved";
        item.AdminReply = adminReply?.Trim();
        item.ResolvedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã phản hồi và đánh dấu hoàn tất.";
        return RedirectToAction(nameof(Index));
    }
}
