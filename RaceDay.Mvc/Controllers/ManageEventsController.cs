using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Organiser")]
    public class ManageEventsController : AppController
    {
        private readonly ApiClient _api;
        private readonly UserSession _session;

        public ManageEventsController(ApiClient api, UserSession session)
        {
            _api = api;
            _session = session;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _api.GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                ViewBag.Error = result.Error;
                return View(new List<OrganiserEventSummaryDto>());
            }

            return View(result.Data ?? new List<OrganiserEventSummaryDto>());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", new EventFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EventFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", model);

            var created = await _api.PostAsync<EventDto>("api/events", ToRequest(model));
            if (SessionEnded(created, out var ended)) return ended;
            if (!created.IsSuccess)
            {
                AddApiErrors(created);
                return View("Form", model);
            }

            var eventId = created.Data!.Id;

            // The banner goes up as a second step, through the API (the website never talks to Azure)
            if (model.BannerFile is { Length: > 0 })
            {
                var banner = await _api.PostFileAsync<EventDto>($"api/events/{eventId}/banner", model.BannerFile);
                if (SessionEnded(banner, out var endedDuringUpload)) return endedDuringUpload;
                if (!banner.IsSuccess)
                {
                    TempData["Error"] = $"Your event was created, but the banner couldn't be uploaded: {banner.Error}";
                    return RedirectToAction(nameof(Edit), new { id = eventId });
                }
            }

            TempData["Success"] = "Event created. Now add its categories so people can enter.";
            return RedirectToAction("Index", "Categories", new { eventId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _api.GetAsync<EventDto>($"api/events/{id}");
            if (SessionEnded(result, out var ended)) return ended;

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.IsNotFound ? "We couldn't find that event." : result.Error;
                return RedirectToAction(nameof(Index));
            }

            var ev = result.Data!;

            // Organisers can only edit their own events
            if (ev.OrganiserId != _session.UserId)
                return RedirectToAction("AccessDenied", "Account");

            return View("Form", new EventFormViewModel
            {
                Id = ev.Id,
                Name = ev.Name,
                Description = ev.Description,
                EventDate = ev.EventDate,
                Location = ev.Location,
                DistanceKm = ev.DistanceKm,
                EventType = ev.EventType,
                BannerImageUrl = ev.BannerImageUrl
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EventFormViewModel model)
        {
            model.Id = id;
            if (!ModelState.IsValid)
                return View("Form", model);

            var updated = await _api.PutAsync<EventDto>($"api/events/{id}", ToRequest(model));
            if (SessionEnded(updated, out var ended)) return ended;
            if (!updated.IsSuccess)
            {
                AddApiErrors(updated);
                return View("Form", model);
            }

            if (model.BannerFile is { Length: > 0 })
            {
                var banner = await _api.PostFileAsync<EventDto>($"api/events/{id}/banner", model.BannerFile);
                if (SessionEnded(banner, out var endedDuringUpload)) return endedDuringUpload;
                if (!banner.IsSuccess)
                {
                    TempData["Error"] = $"Your changes were saved, but the banner couldn't be uploaded: {banner.Error}";
                    return RedirectToAction(nameof(Edit), new { id });
                }
            }

            TempData["Success"] = "Event updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _api.DeleteAsync($"api/events/{id}");
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = "Event deleted.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index));
        }

        private static object ToRequest(EventFormViewModel m) => new
        {
            name = m.Name.Trim(),
            description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim(),
            eventDate = m.EventDate.Date,
            location = m.Location.Trim(),
            distanceKm = m.DistanceKm,
            eventType = m.EventType
        };
    }
}
