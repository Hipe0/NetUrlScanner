using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetURLScanner.Data;
using NetURLScanner.Models;
using NetUrlScanner.Models;
using System.Diagnostics;

namespace NetUrlScanner.Controllers
{
    [Route("")]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
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
        public async Task<IActionResult> Privacy()
        {
            var content = await _context.SiteContents.AsNoTracking()
                .FirstOrDefaultAsync(x => x.ContentKey == "introduction");
            return View(content);
        }

        [HttpGet("FAQ")]
        public async Task<IActionResult> Faq()
        {
            var items = await _context.FaqItems.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            return View(items);
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
