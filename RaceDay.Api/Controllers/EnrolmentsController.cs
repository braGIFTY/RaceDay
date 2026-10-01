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

        [HttpPost("api/enrolments")]
        [RequireRole("Participant")]
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

        [HttpGet("api/enrolments/me")]
        [RequireRole("Participant")]
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

        [HttpDelete("api/enrolments/{id}")]
        [RequireRole("Participant")]
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

        [HttpGet("api/events/{eventId}/enrolments")]
        [RequireRole("Organiser")]
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