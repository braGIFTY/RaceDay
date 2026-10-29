using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
 
namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private static readonly string[] EventTypes = { "Run", "Walk", "Cycle" };
 
        private readonly RaceDayDbContext _context;
        private readonly ImageUploader _imageUploader;
 
        public EventsController(RaceDayDbContext context, ImageUploader imageUploader)
        {
            _context = context;
            _imageUploader = imageUploader;
        }
 
        /// <summary>Lists events, earliest first. Every filter is optional and filters can be combined.</summary>
        /// <param name="upcoming">When true, only events dated today or later.</param>
        /// <param name="type">Only events of this type: Run, Walk or Cycle (not case-sensitive).</param>
        /// <param name="search">Only events whose name or location contains this text (not case-sensitive).</param>
        /// <param name="minDistance">Only events of at least this many kilometres.</param>
        /// <param name="maxDistance">Only events of at most this many kilometres.</param>
        /// <response code="200">Array of matching events, earliest first.</response>
        /// <response code="400">An unknown event type, or minDistance greater than maxDistance.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetAll(
            [FromQuery] bool? upcoming,
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] decimal? minDistance,
            [FromQuery] decimal? maxDistance)
        {
            // An empty value (for example an unselected dropdown on a form) means "no filter"
            string? canonicalType = null;
            if (!string.IsNullOrWhiteSpace(type))
            {
                canonicalType = EventTypes.FirstOrDefault(
                    t => string.Equals(t, type.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonicalType == null)
                    return BadRequest(new { message = "type must be Run, Walk, or Cycle." });
            }
 
            if (minDistance.HasValue && maxDistance.HasValue && minDistance > maxDistance)
                return BadRequest(new { message = "minDistance cannot be greater than maxDistance." });
 
            var query = _context.Events.AsQueryable();
 
            if (upcoming == true)
            {
                var today = DateTime.Today;
                query = query.Where(e => e.EventDate >= today);
            }
 
            if (canonicalType != null)
                query = query.Where(e => e.EventType == canonicalType);
 
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(e => e.Name.ToLower().Contains(term) || e.Location.ToLower().Contains(term));
            }
 
            if (minDistance.HasValue)
                query = query.Where(e => e.DistanceKm >= minDistance.Value);
 
            if (maxDistance.HasValue)
                query = query.Where(e => e.DistanceKm <= maxDistance.Value);
 
            var events = await query
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.Id)
                .Select(e => new EventResponse(e.Id, e.OrganiserId, e.Name, e.Description,
                    e.EventDate, e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl, e.CreatedAt))
                .ToListAsync();
 
            return Ok(events);
        }
 
        /// <summary>(Part 3 addition) Lists the logged-in Organiser's own events, earliest first, each with its number of active (non-cancelled) enrolments. Used for the Organiser dashboard and events page.</summary>
        /// <response code="200">Array of this organiser's events with enrolment counts.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not as an Organiser.</response>
        [HttpGet("mine")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(List<OrganiserEventSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetMine()
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;
 
            var events = await _context.Events
                .Where(e => e.OrganiserId == organiserId)
                .OrderBy(e => e.EventDate)
                .ThenBy(e => e.Id)
                .Select(e => new OrganiserEventSummary(
                    e.Id, e.Name, e.Description, e.EventDate, e.Location,
                    e.DistanceKm, e.EventType, e.BannerImageUrl, e.CreatedAt,
                    e.Enrolments.Count(en => en.Status != "Cancelled")))
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
 
            return Ok(ToResponse(ev));
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
 
            return StatusCode(201, ToResponse(ev));
        }
 
        /// <summary>Updates an event owned by the logged-in Organiser. The banner image is changed separately, through the banner endpoint.</summary>
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
 
            return Ok(ToResponse(ev));
        }
 
        /// <summary>(Part 3 addition) Uploads or replaces the banner image of an event owned by the logged-in Organiser. The image is stored in Azure Blob Storage and its public URL is saved on the event.</summary>
        /// <param name="id">The event's id.</param>
        /// <param name="file">The image: JPEG, PNG or WebP, up to 5 MB.</param>
        /// <response code="200">Banner saved. Returns the updated event, including the image URL.</response>
        /// <response code="400">No file, a file over 5 MB, or a file that is not a JPEG, PNG or WebP image.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        /// <response code="503">Image storage is not set up, or could not be reached.</response>
        [HttpPost("{id}/banner")]
        [RequireRole("Organiser")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UploadBanner(int id, IFormFile? file)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;
 
            var ev = await _context.Events.FindAsync(id);
            if (ev == null)
                return NotFound(new { message = "Event not found." });
 
            if (ev.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own this event." });
 
            var upload = await _imageUploader.UploadAsync(file, "events");
            if (!upload.Succeeded)
                return StatusCode(upload.StatusCode, new { message = upload.Error });
 
            var previousBanner = ev.BannerImageUrl;
            ev.BannerImageUrl = upload.Url;
            await _context.SaveChangesAsync();
 
            // The old banner is no longer used by anything, so remove it from storage
            await _imageUploader.DeleteAsync(previousBanner);
 
            return Ok(ToResponse(ev));
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
 
            var banner = ev.BannerImageUrl;
 
            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
 
            // The event is gone, so its banner image would only be left behind in storage
            await _imageUploader.DeleteAsync(banner);
 
            return NoContent();
        }
 
        private static EventResponse ToResponse(Event ev) =>
            new(ev.Id, ev.OrganiserId, ev.Name, ev.Description, ev.EventDate, ev.Location,
                ev.DistanceKm, ev.EventType, ev.BannerImageUrl, ev.CreatedAt);
    }
}