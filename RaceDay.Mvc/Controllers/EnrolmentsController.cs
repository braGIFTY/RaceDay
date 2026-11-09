using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    // The organiser's view of who has entered an event (a participant's own list is MyEvents)
    [RequireRole("Organiser")]
    public class EnrolmentsController : AppController
    {
        private readonly ApiClient _api;

        public EnrolmentsController(ApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(int? eventId)
        {
            var events = await _api.GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
            if (SessionEnded(events, out var ended)) return ended;

            var model = new EnrolmentsViewModel();
            if (!events.IsSuccess)
            {
                model.Error = events.Error;
                return View(model);
            }

            model.Events = events.Data ?? new List<OrganiserEventSummaryDto>();
            model.SelectedEventId = model.Events.Any(e => e.Id == eventId) ? eventId : DefaultEventId(model.Events);

            if (model.SelectedEventId == null)
                return View(model);

            var enrolments = await _api.GetAsync<List<EnrolmentDto>>($"api/events/{model.SelectedEventId}/enrolments");
            if (SessionEnded(enrolments, out var endedLoading)) return endedLoading;

            if (enrolments.IsSuccess)
                model.Enrolments = enrolments.Data ?? new List<EnrolmentDto>();
            else
                model.Error = enrolments.Error;

            return View(model);
        }
    }
}
