using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetURLScanner.Options;
using System.Security.Claims;

namespace NetURLScanner.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly GoogleAuthOptions _googleAuth;

        public AccountController(ApplicationDbContext context, IOptions<GoogleAuthOptions> googleAuth)
        {
            _context = context;
            _googleAuth = googleAuth.Value;
        }

        private bool IsGoogleAuthEnabled =>
            _googleAuth.Enabled &&
            !string.IsNullOrWhiteSpace(_googleAuth.ClientId) &&
            !string.IsNullOrWhiteSpace(_googleAuth.ClientSecret);

        [HttpGet]
        public IActionResult Login(string returnUrl = "/")
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleAuthEnabled = IsGoogleAuthEnabled;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string returnUrl = "/")
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleAuthEnabled = IsGoogleAuthEnabled;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập Email và Mật khẩu.";
                return View();
            }

            email = email.Trim();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            if (!user.IsActive)
            {
                ViewBag.Error = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
                return View();
            }

            if (string.IsNullOrEmpty(user.PasswordHash))
            {
                ViewBag.Error = IsGoogleAuthEnabled
                    ? "Tài khoản này đăng nhập bằng Google. Vui lòng dùng nút bên dưới."
                    : "Tài khoản này không có mật khẩu cục bộ.";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);

            if (result == PasswordVerificationResult.Failed)
            {
                ViewBag.Error = "Tài khoản hoặc mật khẩu không đúng.";
                return View();
            }

            await SignInAppUserAsync(user);
            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public IActionResult GoogleLogin(string returnUrl = "/")
        {
            if (!IsGoogleAuthEnabled)
            {
                TempData["Error"] = "Đăng nhập Google chưa được cấu hình.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var props = new AuthenticationProperties
            {
                RedirectUri = Url.Action(nameof(GoogleCallback), new { returnUrl })
            };
            return Challenge(props, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleCallback(string returnUrl = "/")
        {
            if (!IsGoogleAuthEnabled)
                return RedirectToAction(nameof(Login), new { returnUrl });

            var email = User.FindFirstValue(ClaimTypes.Email);
            var googleId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var fullName = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(googleId))
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["Error"] = "Google không trả về email. Vui lòng thử lại.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            email = email.Trim();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleSubjectId == googleId);

            if (user == null)
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

                if (user != null)
                {
                    if (!string.IsNullOrEmpty(user.GoogleSubjectId) && user.GoogleSubjectId != googleId)
                    {
                        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        TempData["Error"] = "Email đã liên kết với tài khoản Google khác.";
                        return RedirectToAction(nameof(Login), new { returnUrl });
                    }

                    user.GoogleSubjectId = googleId;
                    if (string.IsNullOrWhiteSpace(user.FullName) && !string.IsNullOrWhiteSpace(fullName))
                        user.FullName = fullName.Trim();
                }
                else
                {
                    user = new User
                    {
                        Email = email,
                        GoogleSubjectId = googleId,
                        FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim(),
                        Role = "User",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.Users.Add(user);
                }

                await _context.SaveChangesAsync();
            }

            if (!user.IsActive)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["Error"] = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            await SignInAppUserAsync(user);
            return RedirectToLocal(returnUrl);
        }

        [HttpGet]
        public IActionResult Register()
        {
            ViewBag.GoogleAuthEnabled = IsGoogleAuthEnabled;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(string email, string password, string confirmPassword)
        {
            ViewBag.GoogleAuthEnabled = IsGoogleAuthEnabled;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ thông tin.";
                return View();
            }

            email = email.Trim();

            if (password.Length < 6)
            {
                ViewBag.Error = "Mật khẩu phải có ít nhất 6 ký tự.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View();
            }

            var existing = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (existing != null)
            {
                ViewBag.Error = string.IsNullOrEmpty(existing.PasswordHash) && IsGoogleAuthEnabled
                    ? "Email đã đăng ký bằng Google. Vui lòng đăng nhập bằng Google."
                    : "Email này đã được sử dụng.";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                Email = email,
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = hasher.HashPassword(user, password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Landing", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private async Task SignInAppUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Email),
                new(ClaimTypes.Email, user.Email),
                new(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Landing", "Home");
        }
    }
}
