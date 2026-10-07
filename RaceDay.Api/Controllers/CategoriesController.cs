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
    public class CategoriesController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public CategoriesController(RaceDayDbContext context)
        {
            _context = context;
        }

        /// <summary>Lists all categories defined for a specific event.</summary>
        /// <param name="eventId">The parent event's id.</param>
        /// <response code="200">Array of categories for this event.</response>
        /// <response code="404">No event exists with this id.</response>
        [HttpGet("api/events/{eventId}/categories")]
        [ProducesResponseType(typeof(List<CategoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByEvent(int eventId)
        {
            var eventExists = await _context.Events.AnyAsync(e => e.Id == eventId);
            if (!eventExists)
                return NotFound(new { message = "Event not found." });

            var categories = await _context.Categories
                .Where(c => c.EventId == eventId)
                .Select(c => new CategoryResponse(c.Id, c.EventId, c.Name, c.Description))
                .ToListAsync();

            return Ok(categories);
        }

        /// <summary>Adds a new category to an event owned by the logged-in Organiser.</summary>
        /// <param name="eventId">The parent event's id.</param>
        /// <param name="request">Category name and description.</param>
        /// <response code="201">Category created successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns this event.</response>
        /// <response code="404">No event exists with this id.</response>
        /// <response code="409">A category with this name already exists on this event.</response>
        [HttpPost("api/events/{eventId}/categories")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(int eventId, CreateCategoryRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var ev = await _context.Events.FindAsync(eventId);
            if (ev == null)
                return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own this event." });

            var duplicateExists = await _context.Categories
                .AnyAsync(c => c.EventId == eventId && c.Name == request.Name);
            if (duplicateExists)
                return Conflict(new { message = "A category with this name already exists for this event." });

            var category = new Category
            {
                EventId = eventId,
                Name = request.Name,
                Description = request.Description
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return StatusCode(201, new CategoryResponse(category.Id, category.EventId, category.Name, category.Description));
        }

        /// <summary>Updates a category belonging to an event owned by the logged-in Organiser.</summary>
        /// <param name="id">The category's id.</param>
        /// <param name="request">Updated name and description.</param>
        /// <response code="200">Category updated successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns the parent event.</response>
        /// <response code="404">No category exists with this id.</response>
        [HttpPut("api/categories/{id}")]
        [RequireRole("Organiser")]
        [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, UpdateCategoryRequest request)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event!.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own the event this category belongs to." });

            category.Name = request.Name;
            category.Description = request.Description;
            await _context.SaveChangesAsync();

            return Ok(new CategoryResponse(category.Id, category.EventId, category.Name, category.Description));
        }

        /// <summary>Deletes a category from an event owned by the logged-in Organiser.</summary>
        /// <param name="id">The category's id.</param>
        /// <response code="204">Category deleted successfully.</response>
        /// <response code="401">No active session.</response>
        /// <response code="403">Logged in, but not the Organiser who owns the parent event.</response>
        /// <response code="404">No category exists with this id.</response>
        [HttpDelete("api/categories/{id}")]
        [RequireRole("Organiser")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(int id)
        {
            var organiserId = HttpContext.Session.GetInt32("UserId")!.Value;

            var category = await _context.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return NotFound(new { message = "Category not found." });

            if (category.Event!.OrganiserId != organiserId)
                return StatusCode(403, new { message = "You do not own the event this category belongs to." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}