using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers;

[Authorize(Roles = "Admin")]
[Route("Admin/Cms")]
public class CmsController : Controller
{
    private readonly ApplicationDbContext _context;

    public CmsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("Introduction")]
    public async Task<IActionResult> EditIntroduction()
    {
        var content = await _context.SiteContents.FirstOrDefaultAsync(x => x.ContentKey == "introduction");
        if (content == null)
        {
            content = new SiteContent { ContentKey = "introduction", Title = "Giới thiệu", Body = "" };
            _context.SiteContents.Add(content);
            await _context.SaveChangesAsync();
        }
        return View(content);
    }

    [HttpPost("Introduction")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditIntroduction(SiteContent model)
    {
        var content = await _context.SiteContents.FirstOrDefaultAsync(x => x.ContentKey == "introduction");
        if (content == null) return NotFound();

        content.Title = model.Title.Trim();
        content.Body = model.Body;
        content.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật trang Giới thiệu.";
        return RedirectToAction(nameof(EditIntroduction));
    }

    [HttpGet("Faq")]
    public async Task<IActionResult> FaqList()
    {
        var items = await _context.FaqItems.OrderBy(x => x.SortOrder).ToListAsync();
        return View(items);
    }

    [HttpGet("Faq/Create")]
    public IActionResult CreateFaq() => View(new FaqItem());

    [HttpPost("Faq/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFaq(FaqItem model)
    {
        if (!ModelState.IsValid) return View(model);
        _context.FaqItems.Add(model);
        await _context.SaveChangesAsync();
        TempData["Success"] = "Đã thêm câu hỏi FAQ.";
        return RedirectToAction(nameof(FaqList));
    }

    [HttpGet("Faq/Edit/{id}")]
    public async Task<IActionResult> EditFaq(int id)
    {
        var item = await _context.FaqItems.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost("Faq/Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditFaq(int id, FaqItem model)
    {
        var item = await _context.FaqItems.FindAsync(id);
        if (item == null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        item.Question = model.Question;
        item.Answer = model.Answer;
        item.SortOrder = model.SortOrder;
        item.IsActive = model.IsActive;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật FAQ.";
        return RedirectToAction(nameof(FaqList));
    }

    [HttpPost("Faq/Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFaq(int id)
    {
        var item = await _context.FaqItems.FindAsync(id);
        if (item != null)
        {
            _context.FaqItems.Remove(item);
            await _context.SaveChangesAsync();
        }
        TempData["Success"] = "Đã xóa FAQ.";
        return RedirectToAction(nameof(FaqList));
    }
}
