using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetURLScanner.Services;
using System.Text.RegularExpressions;

namespace NetURLScanner.Controllers
{
    [Route("Whitelist")]
    public class TrustedBrandsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TrustedBrandsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var brands = await _context.TrustedBrands
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(brands);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            LoadCategories();
            return View(new TrustedBrand());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TrustedBrand model)
        {
            LoadCategories();

            model.BrandName = NormalizeBrandName(model.BrandName);

            var validation = ValidateDomain(model.OfficialDomain);

            if (!validation.IsValid)
            {
                ModelState.AddModelError(nameof(model.OfficialDomain), validation.Message);
                return View(model);
            }

            model.OfficialDomain = validation.NormalizedDomain;

            if (string.IsNullOrWhiteSpace(model.BrandName))
            {
                ModelState.AddModelError(nameof(model.BrandName), "Vui lòng nhập tên thương hiệu.");
                return View(model);
            }

            bool existsInDefaultList = TrustedBrandDefaults.GetDefaultBrands()
                .Any(x => x.Value.Equals(model.OfficialDomain, StringComparison.OrdinalIgnoreCase));

            if (existsInDefaultList)
            {
                ModelState.AddModelError(
                    nameof(model.OfficialDomain),
                    $"Domain {model.OfficialDomain} đã có trong danh sách mặc định của hệ thống.");

                return View(model);
            }

            bool existsInDatabase = await _context.TrustedBrands
                .AnyAsync(x => x.OfficialDomain == model.OfficialDomain);

            if (existsInDatabase)
            {
                ModelState.AddModelError(
                    nameof(model.OfficialDomain),
                    $"Domain {model.OfficialDomain} đã được lưu trong hệ thống.");

                return View(model);
            }

            bool brandNameExists = await _context.TrustedBrands
                .AnyAsync(x => x.BrandName == model.BrandName);

            if (brandNameExists)
            {
                ModelState.AddModelError(
                    nameof(model.BrandName),
                    $"Thương hiệu {model.BrandName} đã tồn tại trong hệ thống.");

                return View(model);
            }

            model.IsActive = true;
            model.CreatedAt = DateTime.Now;

            _context.TrustedBrands.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm domain uy tín {model.OfficialDomain} vào hệ thống.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var brand = await _context.TrustedBrands.FindAsync(id);

            if (brand == null)
            {
                return NotFound();
            }

            brand.IsActive = !brand.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = brand.IsActive
                ? $"Đã kích hoạt domain {brand.OfficialDomain}."
                : $"Đã tạm tắt domain {brand.OfficialDomain}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.TrustedBrands.FindAsync(id);

            if (brand == null)
            {
                return NotFound();
            }

            _context.TrustedBrands.Remove(brand);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa domain {brand.OfficialDomain} khỏi hệ thống.";

            return RedirectToAction(nameof(Index));
        }

        private void LoadCategories()
        {
            ViewBag.Categories = new List<string>
            {
                "Banking",
                "E-Wallet",
                "Payment Gateway",
                "E-Commerce",
                "Securities",
                "Public Service",
                "Social Network",
                "Technology",
                "Logistics",
                "Telecom",
                "Other"
            };
        }

        private string NormalizeBrandName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return input.Trim().ToLower();
        }

        private (bool IsValid, string Message, string NormalizedDomain) ValidateDomain(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return (false, "Vui lòng nhập domain.", "");
            }

            string domain = input.Trim().ToLower();

            if (domain.StartsWith("http://") || domain.StartsWith("https://"))
            {
                return (false, "Chỉ nhập domain, không nhập giao thức http:// hoặc https://. Ví dụ đúng: vietcombank.com.vn", "");
            }

            if (domain.Contains("/"))
            {
                return (false, "Chỉ nhập domain chính, không nhập đường dẫn phía sau. Ví dụ đúng: vietcombank.com.vn", "");
            }

            if (domain.Contains("?"))
            {
                return (false, "Domain không được chứa query string. Vui lòng bỏ phần sau dấu ?.", "");
            }

            if (domain.Contains("#"))
            {
                return (false, "Domain không được chứa fragment. Vui lòng bỏ phần sau dấu #.", "");
            }

            if (domain.Contains(" "))
            {
                return (false, "Domain không được chứa khoảng trắng.", "");
            }

            if (domain.Contains(".."))
            {
                return (false, "Domain không được chứa hai dấu chấm liên tiếp.", "");
            }

            if (domain.StartsWith(".") || domain.EndsWith("."))
            {
                return (false, "Domain không được bắt đầu hoặc kết thúc bằng dấu chấm.", "");
            }

            if (domain.StartsWith("-") || domain.EndsWith("-"))
            {
                return (false, "Domain không được bắt đầu hoặc kết thúc bằng dấu gạch ngang.", "");
            }

            if (domain.StartsWith("www."))
            {
                domain = domain.Substring(4);
            }

            if (!domain.Contains("."))
            {
                return (false, "Domain phải có phần mở rộng, ví dụ: .vn, .com, .com.vn.", "");
            }

            var labels = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (labels.Length < 2)
            {
                return (false, "Domain phải có ít nhất hai phần, ví dụ: momo.vn hoặc vietcombank.com.vn.", "");
            }

            foreach (var label in labels)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return (false, "Domain có phần rỗng giữa các dấu chấm.", "");
                }

                if (label.StartsWith("-") || label.EndsWith("-"))
                {
                    return (false, "Mỗi phần của domain không được bắt đầu hoặc kết thúc bằng dấu gạch ngang.", "");
                }

                bool isValidLabel = label.All(c =>
                    char.IsLetterOrDigit(c) || c == '-');

                if (!isValidLabel)
                {
                    return (false, "Domain chỉ được chứa chữ cái, chữ số, dấu chấm và dấu gạch ngang.", "");
                }
            }

            string tld = labels[^1];

            if (tld.Length < 2)
            {
                return (false, "Phần mở rộng domain không hợp lệ.", "");
            }

            bool isValidDomainFormat = Regex.IsMatch(
                domain,
                @"^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$");

            if (!isValidDomainFormat)
            {
                return (false, "Domain không đúng định dạng chuẩn.", "");
            }

            return (true, "Domain hợp lệ.", domain);
        }
    }
}