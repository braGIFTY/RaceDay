using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Participant")]
    public class MyEventsController : AppController
    {
        private readonly ApiClient _api;

        public MyEventsController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<List<EnrolmentDto>>("api/enrolments/me");
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                ViewBag.Error = result.Error;
                return View(new List<EnrolmentDto>());
            }

            return View(result.Data ?? new List<EnrolmentDto>());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var result = await _api.DeleteAsync($"api/enrolments/{id}");
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = "Your entry was cancelled.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index));
        }
    }
}
