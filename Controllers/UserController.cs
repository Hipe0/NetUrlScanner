using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Services;

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
