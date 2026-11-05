using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;
 
namespace RaceDay.Mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApiClient _api;
 
        public HomeController(ApiClient api)
        {
            _api = api;
        }
 
        // Temporary connection check: lists upcoming events straight from the API.
        // The real branded home page replaces this in the next step.
        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<List<EventDto>>("api/events?upcoming=true");
 
            ViewBag.ApiError = result.IsSuccess ? null : result.Error;
 
            return View(result.IsSuccess ? result.Data! : new List<EventDto>());
        }
 
        public IActionResult Privacy()
        {
            return View();
        }
 
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}