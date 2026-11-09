using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    // The participant's side of events: browse and filter, see the details, and enter
    public class EventsController : AppController
    {
        private readonly ApiClient _api;
        private readonly UserSession _session;

        public EventsController(ApiClient api, UserSession session)
        {
            _api = api;
            _session = session;
        }

        [RequireRole("Participant")]
        public async Task<IActionResult> Index(string? search, string? type, decimal? minDistance, decimal? maxDistance)
        {
            // The filtering itself happens in the API, so the website holds no event logic of its own
            var query = new List<string> { "upcoming=true" };
            if (!string.IsNullOrWhiteSpace(type)) query.Add("type=" + Uri.EscapeDataString(type.Trim()));
            if (!string.IsNullOrWhiteSpace(search)) query.Add("search=" + Uri.EscapeDataString(search.Trim()));
            if (minDistance.HasValue) query.Add("minDistance=" + minDistance.Value.ToString(CultureInfo.InvariantCulture));
            if (maxDistance.HasValue) query.Add("maxDistance=" + maxDistance.Value.ToString(CultureInfo.InvariantCulture));

            var result = await _api.GetAsync<List<EventDto>>("api/events?" + string.Join("&", query));
            if (SessionEnded(result, out var ended)) return ended;

            var model = new EventsIndexViewModel
            {
                Search = search,
                Type = type,
                MinDistance = minDistance,
                MaxDistance = maxDistance
            };

            if (result.IsSuccess)
                model.Events = result.Data ?? new List<EventDto>();
            else
                model.Error = result.Error;

            return View(model);
        }

        [RequireLogin]
        public async Task<IActionResult> Details(int id)
        {
            var ev = await _api.GetAsync<EventDto>($"api/events/{id}");
            if (SessionEnded(ev, out var ended)) return ended;

            if (!ev.IsSuccess)
            {
                TempData["Error"] = ev.IsNotFound ? "We couldn't find that event." : ev.Error;
                return RedirectToAction("Index", "Home");
            }

            var model = new EventDetailsViewModel
            {
                Event = ev.Data!,
                IsParticipant = _session.IsParticipant
            };

            var categories = await _api.GetAsync<List<CategoryDto>>($"api/events/{id}/categories");
            if (SessionEnded(categories, out var endedCategories)) return endedCategories;
            if (categories.IsSuccess)
                model.Categories = categories.Data ?? new List<CategoryDto>();

            // Has this participant already entered (or cancelled)?
            if (_session.IsParticipant)
            {
                var mine = await _api.GetAsync<List<EnrolmentDto>>("api/enrolments/me");
                if (SessionEnded(mine, out var endedMine)) return endedMine;
                if (mine.IsSuccess)
                    model.MyEnrolment = mine.Data?.FirstOrDefault(e => e.EventId == id);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequireRole("Participant")]
        public async Task<IActionResult> Enrol(int eventId, int categoryId)
        {
            if (categoryId <= 0)
            {
                TempData["Error"] = "Please choose a category to enter.";
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            var result = await _api.PostAsync<EnrolmentDto>("api/enrolments", new { eventId, categoryId });
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id = eventId });
            }

            TempData["Success"] = $"You're in! You've entered {result.Data!.EventName}.";
            return RedirectToAction("Index", "MyEvents");
        }
    }
}
