using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Participant")]
    public class MyResultsController : AppController
    {
        private readonly ApiClient _api;

        public MyResultsController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<List<ResultDto>>("api/results/me");
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                ViewBag.Error = result.Error;
                return View(new List<ResultDto>());
            }

            return View(result.Data ?? new List<ResultDto>());
        }
    }
}
