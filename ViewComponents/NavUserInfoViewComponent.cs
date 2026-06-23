using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using System.Security.Claims;

namespace NetURLScanner.ViewComponents
{
    /// <summary>Navbar: đọc IsPremium từ DB (không phụ thuộc claim cookie có thể cũ).</summary>
    public class NavUserInfoViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NavUserInfoViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
                return Content(string.Empty);

            var email = user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Email) ?? "";
            var isPremium = false;

            var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idStr, out var userId))
            {
                isPremium = await _context.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.IsPremium)
                    .FirstOrDefaultAsync();
            }

            return View(new NavUserInfoModel(email, isPremium));
        }
    }

    public record NavUserInfoModel(string Email, bool IsPremium);

}
