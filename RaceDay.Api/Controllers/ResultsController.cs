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
    public class ResultsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public ResultsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Captures a finish time and position for a participant's enrolment, entered by the owning Organiser.</summary>
        /// <param name="id">The enrolment's id.</param>
        /// <param name="request">Finish time and finishing position.</param>
        /// <response code="201">Result recorded successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns the event this enrolment belongs to.</response>
        /// <response code="404">No enrolment exists with this id.</response>
        /// <response code="409">A result has already been captured for this enrolment.</response>
        [HttpPost("api/enrolments/{id}/results")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(int id, CreateResultRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var enrolment = await _context.Enrolments
                .Include(en => en.Event)
                .Include(en => en.Participant)
                .Include(en => en.Result)
                .FirstOrDefaultAsync(en => en.Id == id);

            if (enrolment == null)
                return NotFound(new { message = "Enrolment not found." });

            if (enrolment.Event!.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own the event this enrolment belongs to." });

            if (enrolment.Result != null)
                return Conflict(new { message = "A result has already been captured for this enrolment." });

            var result = new Result
            {
                EnrolmentId = id,
                FinishTime = request.FinishTime,
                Position = request.Position
            };

            _context.Results.Add(result);
            await _context.SaveChangesAsync();

            return StatusCode(201, new ResultResponse(
                result.Id, id, enrolment.ParticipantId, enrolment.Participant!.FullName,
                enrolment.EventId, enrolment.Event.Name, result.FinishTime, result.Position, result.CreatedAt));
        }

        /// <summary>(Additional) Updates a previously captured result.</summary>
        /// <param name="id">The result's id.</param>
        /// <param name="request">Updated finish time and finishing position.</param>
        /// <response code="200">Result updated successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns the event this result belongs to.</response>
        /// <response code="404">No result exists with this id.</response>
        [HttpPut("api/results/{id}")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, UpdateResultRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var result = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(en => en!.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(en => en!.Participant)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (result == null)
                return NotFound(new { message = "Result not found." });

            if (result.Enrolment!.Event!.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own the event this result belongs to." });

            result.FinishTime = request.FinishTime;
            result.Position = request.Position;
            await _context.SaveChangesAsync();

            return Ok(new ResultResponse(
                result.Id, result.EnrolmentId, result.Enrolment.ParticipantId, result.Enrolment.Participant!.FullName,
                result.Enrolment.EventId, result.Enrolment.Event.Name, result.FinishTime, result.Position, result.CreatedAt));
        }

        /// <summary>Lists the logged-in Participant's own results.</summary>
        /// <response code="200">Array of this participant's results.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not as a Participant.</response>
        [HttpGet("api/results/me")]
        [RequireRole("Participant")]
        [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMine()
        {
            var participantId = HttpContext.Session.GetInt32("UserId")!.Value;

            var results = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(en => en!.Event)
                .Include(r => r.Enrolment)
                    .ThenInclude(en => en!.Participant)
                .Where(r => r.Enrolment!.ParticipantId == participantId)
                .Select(r => new ResultResponse(
                    r.Id, r.EnrolmentId, r.Enrolment!.ParticipantId, r.Enrolment.Participant!.FullName,
                    r.Enrolment.EventId, r.Enrolment.Event!.Name, r.FinishTime, r.Position, r.CreatedAt))
                .ToListAsync();

            return Ok(results);
        }

        /// <summary>(Additional) Lists all results for an event owned by the logged-in Organiser.</summary>
        /// <param name="eventId">The event's id.</param>
        /// <response code="200">Array of results for this event.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpGet("api/events/{eventId}/results")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(List<ResultResponse>), StatusCodes.Status200OK)]
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

            var results = await _context.Results
                .Include(r => r.Enrolment)
                    .ThenInclude(en => en!.Participant)
                .Where(r => r.Enrolment!.EventId == eventId)
                .Select(r => new ResultResponse(
                    r.Id, r.EnrolmentId, r.Enrolment!.ParticipantId, r.Enrolment.Participant!.FullName,
                    eventId, ev.Name, r.FinishTime, r.Position, r.CreatedAt))
                .ToListAsync();

            return Ok(results);
        }
    }
}