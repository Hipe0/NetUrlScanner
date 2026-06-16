using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;

namespace NetURLScanner.Controllers.Api
{
    [ApiController]
    [Route("api/trusted-brands")]
    public class TrustedBrandsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TrustedBrandsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/trusted-brands
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetAll()
        {
            var brands = await _context.TrustedBrands
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return Ok(brands);
        }

        // GET: api/trusted-brands/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetById(int id)
        {
            var brand = await _context.TrustedBrands.FindAsync(id);

            if (brand == null)
            {
                return NotFound(new { message = $"Không tìm thấy thương hiệu với ID = {id}" });
            }

            return Ok(brand);
        }

        // POST: api/trusted-brands
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create([FromBody] TrustedBrand brand)
        {
            if (brand == null)
            {
                return BadRequest(new { message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            // Xóa Id nếu được gửi kèm để tránh xung đột tự tăng
            brand.Id = 0;
            brand.CreatedAt = DateTime.Now;

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Chuẩn hóa tên miền (bỏ http/https, www nếu có)
            brand.OfficialDomain = CleanDomain(brand.OfficialDomain);

            // Kiểm tra trùng lặp
            var exists = await _context.TrustedBrands.AnyAsync(x => x.OfficialDomain == brand.OfficialDomain);
            if (exists)
            {
                return BadRequest(new { message = "Thương hiệu với domain này đã tồn tại." });
            }

            _context.TrustedBrands.Add(brand);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = brand.Id }, brand);
        }

        // PUT: api/trusted-brands/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Update(int id, [FromBody] TrustedBrand updatedBrand)
        {
            if (updatedBrand == null)
            {
                return BadRequest(new { message = "Dữ liệu cập nhật không hợp lệ." });
            }

            var existingBrand = await _context.TrustedBrands.FindAsync(id);
            if (existingBrand == null)
            {
                return NotFound(new { message = $"Không tìm thấy thương hiệu với ID = {id}" });
            }

            // Cập nhật các trường
            existingBrand.BrandName = updatedBrand.BrandName;
            existingBrand.OfficialDomain = CleanDomain(updatedBrand.OfficialDomain);
            existingBrand.Category = updatedBrand.Category;
            existingBrand.IsActive = updatedBrand.IsActive;

            // Validate lại model
            if (!TryValidateModel(existingBrand))
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra trùng lặp với thương hiệu khác
            var exists = await _context.TrustedBrands.AnyAsync(x => x.OfficialDomain == existingBrand.OfficialDomain && x.Id != id);
            if (exists)
            {
                return BadRequest(new { message = "Thương hiệu với domain này đã tồn tại dưới ID khác." });
            }

            _context.Entry(existingBrand).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(existingBrand);
        }

        // DELETE: api/trusted-brands/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int id)
        {
            var brand = await _context.TrustedBrands.FindAsync(id);

            if (brand == null)
            {
                return NotFound(new { message = $"Không tìm thấy thương hiệu với ID = {id}" });
            }

            _context.TrustedBrands.Remove(brand);
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
