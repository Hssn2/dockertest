using System.Diagnostics;
using dockertest.Models;
using Microsoft.AspNetCore.Mvc;

namespace dockertest.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;

        public HomeController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IActionResult Index()
        {
            ViewBag.AppVersion = Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev";
            return View();
        }

        public IActionResult Guncelleme()
        {
            ViewBag.AgentUrl = _configuration["UpdateAgent:Url"] ?? "http://localhost:8080";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
