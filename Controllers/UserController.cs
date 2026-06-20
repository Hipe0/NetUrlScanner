using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly AdminSeedService _adminSeed;

        public UserController(ApplicationDbContext context, AdminSeedService adminSeed)
        {
            _context = context;
            _adminSeed = adminSeed;
        }

        public async Task<IActionResult> Index(string? search, string? role)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.Email.Contains(search) || (u.FullName != null && u.FullName.Contains(search)));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);

            ViewBag.Search = search;
            ViewBag.Role = role;
            ViewBag.ProtectedAdminEmail = _adminSeed.ProtectedAdminEmail;

            var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string email, string password, string role, string? fullName)
        {
            email = email?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Email và mật khẩu là bắt buộc.";
                return View();
            }

            if (role != "Manager" && role != "User")
            {
                ViewBag.Error = "Chỉ được tạo tài khoản Manager hoặc User.";
                return View();
            }

            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                ViewBag.Error = "Email đã tồn tại.";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var user = new User
            {
                Email = email,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim(),
                Role = role,
                IsActive = true,
                IsPremium = (role == "Manager" || role == "Admin"),
                CreatedAt = DateTime.Now
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo tài khoản {email} ({role}).";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> UserScans(int id, int page = 1)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            var query = _context.UrlScans.Where(x => x.UserId == id).AsNoTracking();

            ViewBag.SafeCount = await query.CountAsync(x => x.RiskLevel == "Safe");
            ViewBag.WarningCount = await query.CountAsync(x => x.RiskLevel == "Warning");
            ViewBag.SuspiciousCount = await query.CountAsync(x => x.RiskLevel == "Suspicious");

            int pageSize = 10;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var scans = await query.OrderByDescending(x => x.ScannedAt)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.UserEmail = user.Email;
            ViewBag.UserId = id;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(scans);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(int id, string role)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                TempData["Error"] = "Người dùng không tồn tại.";
                return RedirectToAction(nameof(Index));
            }

            if (_adminSeed.IsProtectedAdmin(user.Email))
            {
                TempData["Error"] = "Không thể thay đổi quyền của tài khoản Admin gốc.";
                return RedirectToAction(nameof(Index));
            }

            if (role == "Admin")
            {
                TempData["Error"] = "Hệ thống chỉ cho phép 1 Admin duy nhất.";
                return RedirectToAction(nameof(Index));
            }

            user.Role = role;
            if (role == "Manager" || role == "Admin")
            {
                user.IsPremium = true;
            }
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã cập nhật quyền của {user.Email} thành {role}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (_adminSeed.IsProtectedAdmin(user.Email))
            {
                TempData["Error"] = "Không thể khóa tài khoản Admin gốc.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = user.IsActive ? $"Đã mở khóa {user.Email}." : $"Đã khóa {user.Email}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (_adminSeed.IsProtectedAdmin(user.Email))
            {
                TempData["Error"] = "Không thể xóa tài khoản Admin gốc.";
                return RedirectToAction(nameof(Index));
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Đã xóa tài khoản {user.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
