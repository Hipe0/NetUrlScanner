using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models.Api;
using NetURLScanner.Services;

namespace NetURLScanner.Controllers.Api.V1;

[ApiController]
[Route("api/v1/scans")]
[Produces("application/json")]
public class ScansController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UrlScannerService _scannerService;
    private readonly ILogger<ScansController> _logger;

    public ScansController(
        ApplicationDbContext context,
        UrlScannerService scannerService,
        ILogger<ScansController> logger)
    {
        _context = context;
        _scannerService = scannerService;
        _logger = logger;
    }

    /// <summary>Quét và phân loại rủi ro một URL.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ScanUrlResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Scan([FromBody] ScanUrlRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var error = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return BadRequest(ApiResponse<object>.Fail(error));
        }

        try
        {
            var result = await _scannerService.ScanAsync(request.Url);

            if (request.SaveToHistory)
            {
                _context.UrlScans.Add(result);
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Ok(ApiResponse<ScanUrlResponse>.Ok(
                ScanDtoMapper.ToResponse(result, request.SaveToHistory),
                "Quét URL thành công."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API scan failed for {Url}", request.Url);
            return BadRequest(ApiResponse<object>.Fail("Không thể quét URL. Vui lòng kiểm tra lại địa chỉ."));
        }
    }

    /// <summary>Lấy danh sách lịch sử quét (có phân trang và lọc).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<ScanUrlResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? riskLevel,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.UrlScans.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Url.Contains(search));

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(riskLevel))
            query = query.Where(x => x.RiskLevel == riskLevel);

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var scans = await query
            .OrderByDescending(x => x.ScannedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var data = scans.Select(s => ScanDtoMapper.ToResponse(s)).ToList();

        return Ok(ApiResponse<List<ScanUrlResponse>>.Ok(
            data,
            $"Tìm thấy {totalItems} kết quả.",
            new ApiMeta
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            }));
    }

    /// <summary>Thống kê tổng quan lịch sử quét.</summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<ScanStatsResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = new ScanStatsResponse
        {
            TotalScans = await _context.UrlScans.CountAsync(cancellationToken),
            SafeCount = await _context.UrlScans.CountAsync(x => x.RiskLevel == "Safe", cancellationToken),
            WarningCount = await _context.UrlScans.CountAsync(x => x.RiskLevel == "Warning", cancellationToken),
            SuspiciousCount = await _context.UrlScans.CountAsync(x => x.RiskLevel == "Suspicious", cancellationToken),
            OnlineCount = await _context.UrlScans.CountAsync(x => x.Status == "Online", cancellationToken),
            OfflineCount = await _context.UrlScans.CountAsync(x => x.Status == "Offline", cancellationToken)
        };

        return Ok(ApiResponse<ScanStatsResponse>.Ok(stats, "Lấy thống kê thành công."));
    }

    /// <summary>Lấy chi tiết một lượt quét theo ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<ScanUrlResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var scan = await _context.UrlScans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (scan == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy kết quả quét với ID {id}."));
        }

        return Ok(ApiResponse<ScanUrlResponse>.Ok(ScanDtoMapper.ToResponse(scan), "Lấy chi tiết thành công."));
    }

    /// <summary>Xóa một kết quả quét khỏi lịch sử.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var scan = await _context.UrlScans.FindAsync([id], cancellationToken);

        if (scan == null)
        {
            return NotFound(ApiResponse<object>.Fail($"Không tìm thấy kết quả quét với ID {id}."));
        }

        _context.UrlScans.Remove(scan);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ApiResponse<object>.Ok(null!, "Đã xóa kết quả quét."));
    }
}
