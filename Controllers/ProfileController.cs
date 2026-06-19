using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers;

[Authorize]
[Route("Profile")]
public class ProfileController : AppControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var userId = user.Id;
        ViewBag.TotalScans = await _context.UrlScans.CountAsync(x => x.UserId == userId);
        ViewBag.SafeCount = await _context.UrlScans.CountAsync(x => x.UserId == userId && x.RiskLevel == "Safe");
        ViewBag.WarningCount = await _context.UrlScans.CountAsync(x => x.UserId == userId && x.RiskLevel == "Warning");
        ViewBag.SuspiciousCount = await _context.UrlScans.CountAsync(x => x.UserId == userId && x.RiskLevel == "Suspicious");

        return View(user);
    }

    [HttpPost("Update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(string? fullName, string? phone)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        user.FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ChangePassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "Vui lòng nhập đầy đủ mật khẩu.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            TempData["Error"] = "Tài khoản Google chưa có mật khẩu. Dùng mục Đặt mật khẩu bên dưới.";
            return RedirectToAction(nameof(Index));
        }

        if (newPassword.Length < 6)
        {
            TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Mật khẩu xác nhận không khớp.";
            return RedirectToAction(nameof(Index));
        }

        var hasher = new PasswordHasher<User>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "Mật khẩu hiện tại không đúng.";
            return RedirectToAction(nameof(Index));
        }

        user.PasswordHash = hasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SetPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(string newPassword, string confirmPassword)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            TempData["Error"] = "Tài khoản đã có mật khẩu. Dùng form đổi mật khẩu.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "Vui lòng nhập mật khẩu mới.";
            return RedirectToAction(nameof(Index));
        }

        if (newPassword.Length < 6)
        {
            TempData["Error"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
            return RedirectToAction(nameof(Index));
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "Mật khẩu xác nhận không khớp.";
            return RedirectToAction(nameof(Index));
        }

        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Đã đặt mật khẩu — bạn có thể đăng nhập bằng email/mật khẩu.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var id = GetCurrentUserId();
        return id == null ? null : await _context.Users.FindAsync(id);
    }
}
