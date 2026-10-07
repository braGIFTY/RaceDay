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
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public EventsController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Lists all events.</summary>
        /// <response code="200">Array of all events in the system.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            var events = await _context.Events
                .Select(e => new EventResponse(e.Id, e.OrganiserId, e.Name, e.Description,
                    e.EventDate, e.Location, e.DistanceKm, e.EventType, e.CreatedAt))
                .ToListAsync();

            return Ok(events);
        }

        /// <summary>Retrieves details for a specific event.</summary>
        /// <param name="id">The event's id.</param>
        /// <response code="200">Event details.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            return Ok(new EventResponse(ev.Id, ev.OrganiserId, ev.Name, ev.Description,
                ev.EventDate, ev.Location, ev.DistanceKm, ev.EventType, ev.CreatedAt));
        }

        /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
        /// <param name="request">Name, description, date, location, distance, and event type.</param>
        /// <response code="201">Event created successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not as an Organiser.</response>
        [HttpPost]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

        /// <summary>Updates an event owned by the logged-in Organiser.</summary>
        /// <param name="id">The event's id.</param>
        /// <param name="request">Updated name, description, date, location, distance, and event type.</param>
        /// <response code="200">Event updated successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpPut("{id}")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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

        /// <summary>Deletes an event owned by the logged-in Organiser.</summary>
        /// <param name="id">The event's id.</param>
        /// <response code="204">Event deleted successfully.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpDelete("{id}")]
        [RequireRole("Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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