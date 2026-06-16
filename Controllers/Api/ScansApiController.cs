using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers.Api
{
    [ApiController]
    [Route("api/scans")]
    public class ScansApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UrlScannerService _scannerService;

        public ScansApiController(ApplicationDbContext context, UrlScannerService scannerService)
        {
            _context = context;
            _scannerService = scannerService;
        }

        // GET: api/scans
        [HttpGet]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> GetAll()
        {
            var scans = await _context.UrlScans
                .AsNoTracking()
                .OrderByDescending(x => x.ScannedAt)
                .Take(20)
                .ToListAsync();

            var resultList = scans.Select(s => new
            {
                id = s.Id,
                url = s.Url,
                status = s.Status,
                statusCode = s.StatusCode,
                responseTimeMs = s.ResponseTimeMs,
                isHttps = s.IsHttps,
                riskScore = s.RiskScore,
                riskLevel = s.RiskLevel,
                reasons = string.IsNullOrWhiteSpace(s.Reasons)
                    ? new List<string>()
                    : s.Reasons.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToList(),
                errorMessage = s.ErrorMessage,
                scannedAt = s.ScannedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                ipAddress = s.IpAddress,
                countryName = s.CountryName,
                countryCode = s.CountryCode,
                city = s.City,
                isp = s.Isp,
                latitude = s.Latitude,
                longitude = s.Longitude
            });

            return Ok(resultList);
        }

        // GET: api/scans/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> GetById(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound(new { message = $"Không tìm thấy kết quả quét với ID = {id}" });
            }

            var reasonsList = string.IsNullOrWhiteSpace(scan.Reasons)
                ? new List<string>()
                : scan.Reasons.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList();

            var response = new
            {
                id = scan.Id,
                url = scan.Url,
                status = scan.Status,
                statusCode = scan.StatusCode,
                responseTimeMs = scan.ResponseTimeMs,
                isHttps = scan.IsHttps,
                riskScore = scan.RiskScore,
                riskLevel = scan.RiskLevel,
                reasons = reasonsList,
                errorMessage = scan.ErrorMessage,
                scannedAt = scan.ScannedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                ipAddress = scan.IpAddress,
                countryName = scan.CountryName,
                countryCode = scan.CountryCode,
                city = scan.City,
                isp = scan.Isp,
                latitude = scan.Latitude,
                longitude = scan.Longitude
            };

            return Ok(response);
        }

        // POST: api/scans — cho phép khách quét URL (giống trang /Scan)
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Create([FromBody] ScanRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Url))
            {
                return BadRequest(new { message = "Vui lòng nhập URL hợp lệ." });
            }

            try
            {
                var result = await _scannerService.ScanAsync(request.Url);

                _context.UrlScans.Add(result);
                await _context.SaveChangesAsync();

                var reasonsList = string.IsNullOrWhiteSpace(result.Reasons)
                    ? new List<string>()
                    : result.Reasons.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrWhiteSpace(r))
                        .ToList();

                var response = new
                {
                    id = result.Id,
                    url = result.Url,
                    status = result.Status,
                    statusCode = result.StatusCode,
                    responseTimeMs = result.ResponseTimeMs,
                    isHttps = result.IsHttps,
                    riskScore = result.RiskScore,
                    riskLevel = result.RiskLevel,
                    reasons = reasonsList,
                    errorMessage = result.ErrorMessage,
                    scannedAt = result.ScannedAt.ToString("dd/MM/yyyy HH:mm:ss"),
                    ipAddress = result.IpAddress,
                    countryName = result.CountryName,
                    countryCode = result.CountryCode,
                    city = result.City,
                    isp = result.Isp,
                    latitude = result.Latitude,
                    longitude = result.Longitude
                };

                return CreatedAtAction(nameof(GetById), new { id = result.Id }, response);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Đã xảy ra lỗi hệ thống khi quét URL." });
            }
        }

        // DELETE: api/scans/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager,User")]
        public async Task<IActionResult> Delete(int id)
        {
            var scan = await _context.UrlScans.FindAsync(id);

            if (scan == null)
            {
                return NotFound(new { message = $"Không tìm thấy kết quả quét với ID = {id}" });
            }

            _context.UrlScans.Remove(scan);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }

    public class ScanRequest
    {
        public string Url { get; set; } = string.Empty;
    }
}
