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

        [HttpGet("api/events/{eventId}/categories")]
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

        [HttpPost("api/events/{eventId}/categories")]
        [RequireRole("Organiser")]
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

        [HttpPut("api/categories/{id}")]
        [RequireRole("Organiser")]
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

        [HttpDelete("api/categories/{id}")]
        [RequireRole("Organiser")]
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