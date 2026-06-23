using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;
using System.Security.Claims;

namespace NetURLScanner.Controllers
{
    /// <summary>Nâng cấp Premium — thanh toán demo VietQR, set User.IsPremium, refresh cookie.</summary>
    [Authorize]
    public class PremiumController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserAuthService _userAuth;

        public PremiumController(ApplicationDbContext context, UserAuthService userAuth)
        {
            _context = context;
            _userAuth = userAuth;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdStr, out int userId))
                {
                    var user = await _context.Users.FindAsync(userId);
                    if (user != null && user.IsPremium)
                    {
                        ViewBag.IsAlreadyPremium = true;
                    }
                }
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutMock()
        {
            var result = await ActivatePremiumAsync();
            if (!result.Success)
            {
                if (result.RequireLogin)
                    return RedirectToAction("Login", "Account", new { returnUrl = "/Premium" });
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Success));
        }

        /// <summary>AJAX kích hoạt Premium — tránh mất cookie/form POST thường gặp khi collapse Bootstrap.</summary>
        [HttpPost("/Premium/CheckoutAjax")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckoutAjax()
        {
            var result = await ActivatePremiumAsync();
            if (!result.Success)
            {
                return Json(new
                {
                    success = false,
                    message = result.Message,
                    requireLogin = result.RequireLogin,
                    loginUrl = Url.Action("Login", "Account", new { returnUrl = "/Premium" })
                });
            }

            return Json(new
            {
                success = true,
                message = result.Message,
                redirectUrl = Url.Action(nameof(Success))
            });
        }

        [HttpGet]
        public async Task<IActionResult> Success()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return RedirectToAction("Login", "Account", new { returnUrl = "/Premium/Success" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null || !user.IsPremium)
                return RedirectToAction(nameof(Index));

            return View();
        }

        private async Task<(bool Success, bool RequireLogin, string Message)> ActivatePremiumAsync()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
                return (false, true, "Vui lòng đăng nhập để nâng cấp Premium.");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return (false, true, "Không tìm thấy tài khoản.");

            if (user.IsPremium)
                return (true, false, "Tài khoản đã là Premium.");

            try
            {
                user.IsPremium = true;
                await _context.SaveChangesAsync();
                await _userAuth.SignInAsync(HttpContext, user);
            }
            catch (Exception)
            {
                return (false, false, "Không thể kích hoạt Premium. Vui lòng thử lại hoặc liên hệ quản trị viên.");
            }

            return (true, false, "Thanh toán thành công! Bạn đã trở thành thành viên Premium.");
        }
    }
}
