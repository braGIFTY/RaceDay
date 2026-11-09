using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Organiser")]
    public class CategoriesController : AppController
    {
        private readonly ApiClient _api;

        public CategoriesController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(int? eventId)
        {
            var events = await _api.GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
            if (SessionEnded(events, out var ended)) return ended;

            var model = new CategoriesViewModel();
            if (!events.IsSuccess)
            {
                model.Error = events.Error;
                return View(model);
            }

            model.Events = events.Data ?? new List<OrganiserEventSummaryDto>();
            model.SelectedEventId = model.Events.Any(e => e.Id == eventId) ? eventId : DefaultEventId(model.Events);

            if (model.SelectedEventId == null)
                return View(model);

            var categories = await _api.GetAsync<List<CategoryDto>>($"api/events/{model.SelectedEventId}/categories");
            if (SessionEnded(categories, out var endedLoading)) return endedLoading;

            if (categories.IsSuccess)
                model.Categories = categories.Data ?? new List<CategoryDto>();
            else
                model.Error = categories.Error;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int eventId, string? name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 50)
            {
                TempData["Error"] = "Please enter a category name of up to 50 characters.";
                return RedirectToAction(nameof(Index), new { eventId });
            }

            var result = await _api.PostAsync<CategoryDto>($"api/events/{eventId}/categories", new
            {
                name = name.Trim(),
                description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            });
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = $"Category \"{name.Trim()}\" added.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index), new { eventId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, int eventId)
        {
            var result = await _api.DeleteAsync($"api/categories/{id}");
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = "Category deleted.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index), new { eventId });
        }
    }
}
