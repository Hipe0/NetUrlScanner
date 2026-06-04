using Microsoft.AspNetCore.Mvc;
using NetUrlScanner.Models;
using System.Diagnostics;

namespace NetUrlScanner.Controllers
{
    [Route("")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        [HttpGet("")]
        public IActionResult Landing()
        {
            return View();
        }

        [HttpGet("Home/Index")]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("Introduction")]
        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet("Home/Error")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet("Home/Error404")]
        public IActionResult Error404()
        {
            return View();
        }
    }
}
