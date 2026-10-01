using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var events = await _context.Events
                .Select(e => new EventResponse(e.Id, e.OrganiserId, e.Name, e.Description,
                    e.EventDate, e.Location, e.DistanceKm, e.EventType, e.CreatedAt))
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            return Ok(new EventResponse(ev.Id, ev.OrganiserId, ev.Name, ev.Description,
                ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, ev.CreatedAt));
        }

        [HttpPost]
        [RequireRole("Organiser")]
        public async Task<IActionResult> Create(CreateEventRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = new Event
            {
                OrganiserId = organiserId,
                Name = request.Name,
                Description = request.Description,
                EventDate = request.EventDate,
                Location = request.Location,
                DistanceKm = request.DistanceKm,
                EventType = request.EventType
            };

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();

            return StatusCode(201, new EventResponse(ev.Id, ev.OrganiserId, ev.Name, ev.Description,
                ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, ev.CreatedAt));
        }

        [HttpPut("{id}")]
        [RequireRole("Organiser")]
        public async Task<IActionResult> Update(int id, UpdateEventRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own this event." });

            ev.Name = request.Name;
            ev.Description = request.Description;
            ev.EventDate = request.EventDate;
            ev.Location = request.Location;
            ev.DistanceKm = request.DistanceKm;
            ev.EventType = request.EventType;
            await _context.SaveChangesAsync();

            return Ok(new EventResponse(ev.Id, ev.OrganiserId, ev.Name, ev.Description,
                ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, ev.CreatedAt));
        }

        [HttpDelete("{id}")]
        [RequireRole("Organiser")]
        public async Task<IActionResult> Delete(int id)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own this event." });

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}