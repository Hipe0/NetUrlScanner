using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers.Api
{
    [ApiController]
    [Route("api/blacklisted-domains")]
    public class BlacklistedDomainsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BlacklistedDomainsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/blacklisted-domains
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var domains = await _context.BlacklistedDomains
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(domains);
        }

        // GET: api/blacklisted-domains/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var domain = await _context.BlacklistedDomains.FindAsync(id);

            if (domain == null)
            {
                return NotFound(new { message = $"Không tìm thấy domain blacklist với ID = {id}" });
            }

            return Ok(domain);
        }

        // POST: api/blacklisted-domains
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BlacklistedDomain domain)
        {
            if (domain == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            // Xóa Id nếu được gửi kèm để tránh xung đột tự tăng
            domain.Id = 0;
            domain.CreatedAt = DateTime.Now;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Chuẩn hóa tên miền (bỏ http/https, www nếu có)
            domain.Domain = CleanDomain(domain.Domain);

            // Kiểm tra trùng lặp
            var exists = await _context.BlacklistedDomains.AnyAsync(x => x.Domain == domain.Domain);
            if (exists)
            {
                return BadRequest(new { message = "Domain này đã tồn tại trong danh sách đen." });
            }

            _context.BlacklistedDomains.Add(domain);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = domain.Id }, domain);
        }

        // PUT: api/blacklisted-domains/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BlacklistedDomain updatedDomain)
        {
            if (updatedDomain == null)
            {
                return BadRequest(new { message = "Dữ liệu cập nhật không hợp lệ." });
            }

            var existingDomain = await _context.BlacklistedDomains.FindAsync(id);
            if (existingDomain == null)
            {
                return NotFound(new { message = $"Không tìm thấy domain blacklist với ID = {id}" });
            }

            // Cập nhật các trường
            existingDomain.Domain = CleanDomain(updatedDomain.Domain);
            existingDomain.Category = updatedDomain.Category;
            existingDomain.Reason = updatedDomain.Reason;
            existingDomain.Severity = updatedDomain.Severity;
            existingDomain.IsActive = updatedDomain.IsActive;

            // Validate lại model
            if (!TryValidateModel(existingDomain))
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra trùng lặp với domain khác
            var exists = await _context.BlacklistedDomains.AnyAsync(x => x.Domain == existingDomain.Domain && x.Id != id);
            if (exists)
            {
                return BadRequest(new { message = "Domain này đã tồn tại trong danh sách đen dưới ID khác." });
            }

            _context.Entry(existingDomain).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(existingDomain);
        }

        // DELETE: api/blacklisted-domains/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var domain = await _context.BlacklistedDomains.FindAsync(id);

            if (domain == null)
            {
                return NotFound(new { message = $"Không tìm thấy domain blacklist với ID = {id}" });
            }

            _context.BlacklistedDomains.Remove(domain);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private string CleanDomain(string rawDomain)
        {
            if (string.IsNullOrWhiteSpace(rawDomain)) return string.Empty;

            var domain = rawDomain.Trim().ToLower();
            if (domain.StartsWith("https://")) domain = domain.Substring(8);
            else if (domain.StartsWith("http://")) domain = domain.Substring(7);

            if (domain.StartsWith("www.")) domain = domain.Substring(4);

            // Cắt bỏ phần đuôi path nếu người dùng nhập cả link (ví dụ: google.com/about/ -> google.com)
            var slashIdx = domain.IndexOf('/');
            if (slashIdx > 0)
            {
                domain = domain.Substring(0, slashIdx);
            }

            return domain;
        }
    }
}
