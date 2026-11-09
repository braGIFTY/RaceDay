using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Organiser")]
    public class DashboardController : AppController
    {
        private readonly ApiClient _api;

        public DashboardController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
            if (SessionEnded(result, out var ended)) return ended;

            var model = new DashboardViewModel();
            if (!result.IsSuccess)
            {
                model.Error = result.Error;
                return View(model);
            }

            var events = result.Data ?? new List<OrganiserEventSummaryDto>();
            var today = DateTime.Today;

            model.Events = events;
            model.UpcomingCount = events.Count(e => e.EventDate.Date >= today);
            model.TotalEnrolments = events.Sum(e => e.EnrolmentCount);
            model.NextEventDate = events
                .Where(e => e.EventDate.Date >= today)
                .OrderBy(e => e.EventDate)
                .Select(e => (DateTime?)e.EventDate)
                .FirstOrDefault();

            return View(model);
        }
    }
}
