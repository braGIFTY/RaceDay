using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    [RequireRole("Organiser")]
    public class ResultsController : AppController
    {
        // hh:mm:ss, for example 01:38:47 ("38:47" would be ambiguous, so it isn't accepted)
        private static readonly Regex TimeFormat = new(@"^\d{1,2}:[0-5]\d:[0-5]\d$", RegexOptions.Compiled);

        private readonly ApiClient _api;

        public ResultsController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(int? eventId)
        {
            var events = await _api.GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
            if (SessionEnded(events, out var ended)) return ended;

            var model = new ResultsViewModel();
            if (!events.IsSuccess)
            {
                model.Error = events.Error;
                return View(model);
            }

            model.Events = events.Data ?? new List<OrganiserEventSummaryDto>();
            model.SelectedEventId = model.Events.Any(e => e.Id == eventId) ? eventId : DefaultEventId(model.Events);
            model.SelectedEvent = model.Events.FirstOrDefault(e => e.Id == model.SelectedEventId);

            if (model.SelectedEvent == null)
                return View(model);

            var enrolments = await _api.GetAsync<List<EnrolmentDto>>($"api/events/{model.SelectedEvent.Id}/enrolments");
            if (SessionEnded(enrolments, out var endedEnrolments)) return endedEnrolments;

            var results = await _api.GetAsync<List<ResultDto>>($"api/events/{model.SelectedEvent.Id}/results");
            if (SessionEnded(results, out var endedResults)) return endedResults;

            if (!enrolments.IsSuccess || !results.IsSuccess)
            {
                model.Error = enrolments.Error ?? results.Error;
                return View(model);
            }

            model.Recorded = results.Data ?? new List<ResultDto>();

            // Everyone still entered who doesn't have a result yet
            var done = model.Recorded.Select(r => r.EnrolmentId).ToHashSet();
            model.Awaiting = (enrolments.Data ?? new List<EnrolmentDto>())
                .Where(e => e.Status == "Confirmed" && !done.Contains(e.Id))
                .ToList();

            // Results can only be captured once the event date has arrived
            model.CanRecord = model.SelectedEvent.EventDate.Date <= DateTime.Today;

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Record(int eventId, int enrolmentId, string? finishTime, int position)
        {
            if (string.IsNullOrWhiteSpace(finishTime) || !TimeFormat.IsMatch(finishTime.Trim()))
            {
                TempData["Error"] = "Enter the finish time as hh:mm:ss, for example 01:38:47.";
                return RedirectToAction(nameof(Index), new { eventId });
            }

            if (position < 1)
            {
                TempData["Error"] = "The finishing position must be 1 or more.";
                return RedirectToAction(nameof(Index), new { eventId });
            }

            var time = TimeSpan.Parse(finishTime.Trim(), CultureInfo.InvariantCulture);

            var result = await _api.PostAsync<ResultDto>($"api/enrolments/{enrolmentId}/results", new { finishTime = time, position });
            if (SessionEnded(result, out var ended)) return ended;

            if (result.IsSuccess)
                TempData["Success"] = "Result recorded.";
            else
                TempData["Error"] = result.Error;

            return RedirectToAction(nameof(Index), new { eventId });
        }
    }
}
