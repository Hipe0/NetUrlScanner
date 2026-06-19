using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace NetURLScanner.Controllers;

public abstract class AppControllerBase : Controller
{
    protected int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    protected string GetCurrentUserEmail() =>
        User.FindFirst(ClaimTypes.Email)?.Value ?? User.Identity?.Name ?? string.Empty;
}
