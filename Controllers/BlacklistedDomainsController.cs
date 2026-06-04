using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetURLScanner.Services;
using System.Text.RegularExpressions;

namespace NetURLScanner.Controllers
{
    [Route("Blacklist")]
    public class BlacklistedDomainsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlacklistedDomainsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var domains = await _context.BlacklistedDomains
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return View(domains);
        }

        [HttpGet("Create")]
        public IActionResult Create()
        {
            LoadOptions();
            return View(new BlacklistedDomain());
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlacklistedDomain model)
        {
            LoadOptions();

            var validation = ValidateDomain(model.Domain);

            if (!validation.IsValid)
            {
                ModelState.AddModelError(nameof(model.Domain), validation.Message);
                return View(model);
            }

            model.Domain = validation.NormalizedDomain;

            if (!ValidateRequiredFields(model))
            {
                return View(model);
            }

            bool existsInBlacklist = await _context.BlacklistedDomains
                .AnyAsync(x => x.Domain == model.Domain);

            if (existsInBlacklist)
            {
                ModelState.AddModelError(
                    nameof(model.Domain),
                    $"Domain {model.Domain} đã tồn tại trong blacklist.");

                return View(model);
            }

            if (await IsTrustedDomainAsync(model.Domain))
            {
                ModelState.AddModelError(
                    nameof(model.Domain),
                    $"Domain {model.Domain} đang thuộc danh sách URL uy tín, không thể thêm vào blacklist.");

                return View(model);
            }

            model.Category = model.Category.Trim();
            model.Severity = model.Severity.Trim();
            model.Reason = model.Reason.Trim();
            model.IsActive = true;
            model.CreatedAt = DateTime.Now;

            _context.BlacklistedDomains.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã thêm domain {model.Domain} vào blacklist.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var domain = await _context.BlacklistedDomains.FindAsync(id);

            if (domain == null)
            {
                return NotFound();
            }

            LoadOptions();
            return View(domain);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlacklistedDomain model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            LoadOptions();

            var domainInDb = await _context.BlacklistedDomains.FindAsync(id);

            if (domainInDb == null)
            {
                return NotFound();
            }

            var validation = ValidateDomain(model.Domain);

            if (!validation.IsValid)
            {
                ModelState.AddModelError(nameof(model.Domain), validation.Message);
                return View(model);
            }

            string normalizedDomain = validation.NormalizedDomain;

            if (!ValidateRequiredFields(model))
            {
                return View(model);
            }

            bool duplicateDomain = await _context.BlacklistedDomains
                .AnyAsync(x => x.Id != id && x.Domain == normalizedDomain);

            if (duplicateDomain)
            {
                ModelState.AddModelError(
                    nameof(model.Domain),
                    $"Domain {normalizedDomain} đã tồn tại trong blacklist.");

                return View(model);
            }

            if (await IsTrustedDomainAsync(normalizedDomain))
            {
                ModelState.AddModelError(
                    nameof(model.Domain),
                    $"Domain {normalizedDomain} đang thuộc danh sách URL uy tín, không thể đưa vào blacklist.");

                return View(model);
            }

            domainInDb.Domain = normalizedDomain;
            domainInDb.Category = model.Category.Trim();
            domainInDb.Severity = model.Severity.Trim();
            domainInDb.Reason = model.Reason.Trim();
            domainInDb.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã cập nhật domain {domainInDb.Domain}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("ToggleStatus/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var domain = await _context.BlacklistedDomains.FindAsync(id);

            if (domain == null)
            {
                return NotFound();
            }

            domain.IsActive = !domain.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = domain.IsActive
                ? $"Đã kích hoạt blacklist cho domain {domain.Domain}."
                : $"Đã tạm tắt blacklist cho domain {domain.Domain}.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var domain = await _context.BlacklistedDomains.FindAsync(id);

            if (domain == null)
            {
                return NotFound();
            }

            _context.BlacklistedDomains.Remove(domain);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Đã xóa domain {domain.Domain} khỏi blacklist.";

            return RedirectToAction(nameof(Index));
        }

        private bool ValidateRequiredFields(BlacklistedDomain model)
        {
            bool isValid = true;

            if (string.IsNullOrWhiteSpace(model.Category))
            {
                ModelState.AddModelError(nameof(model.Category), "Vui lòng chọn loại rủi ro.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(model.Severity))
            {
                ModelState.AddModelError(nameof(model.Severity), "Vui lòng chọn mức độ rủi ro.");
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(model.Reason))
            {
                ModelState.AddModelError(nameof(model.Reason), "Vui lòng nhập lý do đưa domain vào blacklist.");
                isValid = false;
            }

            return isValid;
        }

        private async Task<bool> IsTrustedDomainAsync(string domain)
        {
            bool existsInTrustedDefaults = TrustedBrandDefaults.GetDefaultBrands()
                .Any(x => x.Value.Equals(domain, StringComparison.OrdinalIgnoreCase));

            if (existsInTrustedDefaults)
            {
                return true;
            }

            bool existsInTrustedDatabase = await _context.TrustedBrands
                .AnyAsync(x => x.OfficialDomain == domain);

            return existsInTrustedDatabase;
        }

        private void LoadOptions()
        {
            ViewBag.Categories = new List<string>
            {
                "Phishing",
                "Gambling",
                "Scam",
                "Malware",
                "Fake Brand",
                "Financial Fraud",
                "Sensitive Content",
                "Other"
            };

            ViewBag.Severities = new List<string>
            {
                "High",
                "Medium",
                "Low"
            };
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
                return (false, "Chỉ nhập domain, không nhập giao thức http:// hoặc https://. Ví dụ đúng: tichphan.vip", "");
            }

            if (domain.Contains("/"))
            {
                return (false, "Chỉ nhập domain chính, không nhập đường dẫn phía sau. Ví dụ đúng: tichphan.vip", "");
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
                return (false, "Domain phải có phần mở rộng, ví dụ: .com, .vn, .xyz, .vip.", "");
            }

            var labels = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);

            if (labels.Length < 2)
            {
                return (false, "Domain phải có ít nhất hai phần, ví dụ: example.com hoặc tichphan.vip.", "");
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