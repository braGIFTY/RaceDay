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

        [HttpPost("api/enrolments/{id}/results")]
        [RequireRole("Organiser")]
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

        [HttpPut("api/results/{id}")]
        [RequireRole("Organiser")]
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

        [HttpGet("api/results/me")]
        [RequireRole("Participant")]
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

        [HttpGet("api/events/{eventId}/results")]
        [RequireRole("Organiser")]
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