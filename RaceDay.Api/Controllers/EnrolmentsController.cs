using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    public class EnrolmentsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EnrolmentsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Enters the logged-in Participant into an event by selecting a category. Records the link between participant, event, and category directly.</summary>
        /// <param name="request">The event and category being entered.</param>
        /// <response code="201">Enrolment created successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not as a Participant.</response>
        /// <response code="404">The event does not exist, or the category does not belong to that event.</response>
        /// <response code="409">Already enrolled in this event.</response>
        [HttpPost("api/enrolments")]
        [RequireRole("Participant")]
        [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(CreateEnrolmentRequest request)
        {
            var participantId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = await _context.Events.FindAsync(request.EventId);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            var category = await _context.Categories.FindAsync(request.CategoryId);
            if (category == null || category.EventId != request.EventId)
                return NotFound(new { message = "Category not found for this event." });

            var alreadyEnrolled = await _context.Enrolments
                .AnyAsync(en => en.ParticipantId == participantId && en.EventId == request.EventId);
            if (alreadyEnrolled)
                return Conflict(new { message = "You are already enrolled in this event." });

            var enrolment = new Enrolment
            {
                ParticipantId = participantId,
                EventId = request.EventId,
                CategoryId = request.CategoryId
            };

            _context.Enrolments.Add(enrolment);
            await _context.SaveChangesAsync();

            var participant = await _context.Participants.FindAsync(participantId);

            return StatusCode(201, new EnrolmentResponse(
                enrolment.Id, participantId, participant!.FullName,
                request.EventId, ev.Name, request.CategoryId, category.Name,
                enrolment.EnrolmentDate, enrolment.Status));
        }

        /// <summary>Lists the logged-in Participant's own enrolments.</summary>
        /// <response code="200">Array of this participant's enrolments.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not as a Participant.</response>
        [HttpGet("api/enrolments/me")]
        [RequireRole("Participant")]
        [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMine()
        {
            var participantId = HttpContext.Session.GetInt32("UserId")!.Value;

            var enrolments = await _context.Enrolments
                .Where(en => en.ParticipantId == participantId)
                .Include(en => en.Participant)
                .Include(en => en.Event)
                .Include(en => en.Category)
                .Select(en => new EnrolmentResponse(
                    en.Id, en.ParticipantId, en.Participant!.FullName,
                    en.EventId, en.Event!.Name, en.CategoryId, en.Category!.Name,
                    en.EnrolmentDate, en.Status))
                .ToListAsync();

            return Ok(enrolments);
        }

        /// <summary>(Additional) Cancels the logged-in Participant's own enrolment.</summary>
        /// <param name="id">The enrolment's id.</param>
        /// <response code="204">Enrolment cancelled successfully.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but this enrolment does not belong to the current Participant.</response>
        /// <response code="404">No enrolment exists with this id.</response>
        [HttpDelete("api/enrolments/{id}")]
        [RequireRole("Participant")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Cancel(int id)
        {
            var participantId = HttpContext.Session.GetInt32("UserId")!.Value;

            var enrolment = await _context.Enrolments.FindAsync(id);
            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.ParticipantId != participantId)
                return StatusCode(403, new { message = "You do not own this enrolment." });

            _context.Enrolments.Remove(enrolment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>Lists all enrolments for an event owned by the logged-in Organiser.</summary>
        /// <param name="eventId">The event's id.</param>
        /// <response code="200">Array of enrolments for this event.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpGet("api/events/{eventId}/enrolments")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(List<EnrolmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own this event." });

            var enrolments = await _context.Enrolments
                .Where(en => en.EventId == eventId)
                .Include(en => en.Participant)
                .Include(en => en.Category)
                .Select(en => new EnrolmentResponse(
                    en.Id, en.ParticipantId, en.Participant!.FullName,
                    en.EventId, ev.Name, en.CategoryId, en.Category!.Name,
                    en.EnrolmentDate, en.Status))
                .ToListAsync();

            return Ok(enrolments);
        }
    }
}