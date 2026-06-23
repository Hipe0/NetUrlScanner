using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NetURLScanner.Models;
using System.Security.Claims;

namespace NetURLScanner.Helpers
{
    /// <summary>Claims khi đăng nhập — navbar đọc IsPremium từ DB qua NavUserInfoViewComponent.</summary>
    public static class AuthSignInHelper
    {
        public const string IsPremiumClaimType = "IsPremium";

        public static List<Claim> BuildClaims(User user) => new()
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new(IsPremiumClaimType, user.IsPremium ? "true" : "false")
        };

        public static async Task SignInAsync(HttpContext httpContext, User user)
        {
            var identity = new ClaimsIdentity(BuildClaims(user), CookieAuthenticationDefaults.AuthenticationScheme);
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }

        public static bool IsPremiumUser(ClaimsPrincipal? user) =>
            user?.FindFirstValue(IsPremiumClaimType) == "true";
    }
}
