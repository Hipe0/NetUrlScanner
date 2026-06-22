using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NetURLScanner.Controllers;

/// <summary>
/// Lớp cơ sở cho Controller — cung cấp helper đọc thông tin user từ cookie đăng nhập.
/// </summary>
public abstract class AppControllerBase : Controller
{
    /// <summary>
    /// Lấy UserId từ claim NameIdentifier (gán khi Login/GoogleLogin thành công).
    /// Trả về null nếu khách chưa đăng nhập.
    /// </summary>
    protected int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    /// <summary>Email hiện tại — dùng điền sẵn form góp ý, profile…</summary>
    protected string GetCurrentUserEmail() =>
        User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty;
}
