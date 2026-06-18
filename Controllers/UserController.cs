using Microsoft.AspNetCore.Authorization;
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

        // GET: Hiển thị danh sách người dùng để phân quyền
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users.ToListAsync();
            ViewBag.ProtectedAdminEmail = _adminSeed.ProtectedAdminEmail;
            return View(users);
        }

        // GET: Hiển thị danh sách kết quả quét của 1 người dùng cụ thể (chỉ Admin truy cập)
        [HttpGet]
        public async Task<IActionResult> UserScans(int id, int page = 1)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var query = _context.UrlScans.Where(x => x.UserId == id).AsNoTracking().AsQueryable();

            // Lấy dữ liệu đếm cho biểu đồ thống kê
            ViewBag.SafeCount = await query.CountAsync(x => x.RiskLevel == "Safe");
            ViewBag.WarningCount = await query.CountAsync(x => x.RiskLevel == "Warning");
            ViewBag.SuspiciousCount = await query.CountAsync(x => x.RiskLevel == "Suspicious");

            // Xử lý phân trang đầu ra
            int pageSize = 10;
            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            if (page < 1) page = 1;
            if (page > totalPages && totalPages > 0) page = totalPages;

            var scans = await query
                .OrderByDescending(x => x.ScannedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.UserEmail = user.Email;
            ViewBag.UserId = id;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(scans);
        }

        // POST: Cập nhật quyền
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
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật quyền của {user.Email} thành {role}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
