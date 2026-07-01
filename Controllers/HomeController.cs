using System.Diagnostics;
using dockertest.Models;
using Microsoft.AspNetCore.Mvc;

namespace dockertest.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewBag.AppVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
            return View();
        }

        public IActionResult Hakkimda()
        {
            return View();
        }

        public IActionResult Projeler()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Guncelleme()
        {
            var url = _configuration["UpdateAgent:Url"] ?? "http://localhost:8080";
            return Redirect(url);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
