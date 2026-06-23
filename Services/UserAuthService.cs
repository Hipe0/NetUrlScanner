using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using System.Security.Claims;

namespace NetURLScanner.Services;

/// <summary>Đăng nhập (cookie claims) + trạng thái Premium cho navbar.</summary>
public class UserAuthService
{
    public const string IsPremiumClaimType = "IsPremium";

    private readonly ApplicationDbContext _context;

    public UserAuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public static List<Claim> BuildClaims(User user) => new()
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.Email),
        new(ClaimTypes.Email, user.Email),
        new(ClaimTypes.Role, user.Role),
        new(IsPremiumClaimType, user.IsPremium ? "true" : "false")
    };

    public async Task SignInAsync(HttpContext httpContext, User user)
    {
        var identity = new ClaimsIdentity(BuildClaims(user), CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    public async Task<NavUserInfo?> GetNavInfoAsync(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return null;

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

        return new NavUserInfo(email, isPremium);
    }
}

public record NavUserInfo(string Email, bool IsPremium);
