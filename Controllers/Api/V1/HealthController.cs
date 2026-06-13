using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models.Api;

namespace NetURLScanner.Controllers.Api.V1;

[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HealthController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>Kiểm tra trạng thái API và kết nối database.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    ApiResponse<object>.Fail("Database không khả dụng."));
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                status = "healthy",
                service = "NetURLScanner API",
                version = "1.0",
                database = "connected",
                timestamp = DateTime.UtcNow
            }, "Hệ thống hoạt động bình thường."));
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.Fail("Hệ thống đang gặp sự cố."));
        }
    }
}
