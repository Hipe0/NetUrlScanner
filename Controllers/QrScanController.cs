using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NetURLScanner.Controllers;

[Authorize(Roles = "Admin,Manager,User")]
[Route("QrScan")]
public class QrScanController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
