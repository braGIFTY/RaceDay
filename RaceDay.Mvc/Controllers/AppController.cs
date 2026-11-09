using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Models;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Controllers
{
    // Small helpers shared by the organiser and participant controllers
    public abstract class AppController : Controller
    {
        // If the API no longer recognises the user's session, send them to log in again
        protected bool SessionEnded(ApiResult result, out IActionResult redirect)
        {
            if (result.IsUnauthorized)
            {
                TempData["Error"] = "Your session has ended. Please log in again.";
                redirect = RedirectToAction("Login", "Account");
                return true;
            }

            redirect = null!;
            return false;
        }

        // Shows whatever the API complained about at the top of the form
        protected void AddApiErrors(ApiResult result)
        {
            if (result.FieldErrors.Count > 0)
            {
                foreach (var message in result.FieldErrors.Values.SelectMany(m => m))
                    ModelState.AddModelError(string.Empty, message);
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "Something went wrong. Please try again.");
            }
        }

        // The event to show first: the next upcoming one, otherwise the most recent (events arrive earliest first)
        protected static int? DefaultEventId(List<OrganiserEventSummaryDto> events)
        {
            var today = DateTime.Today;
            return (events.FirstOrDefault(e => e.EventDate.Date >= today) ?? events.LastOrDefault())?.Id;
        }
    }
}
