using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Filters;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly RaceDayDbContext _context;

        public UsersController(RaceDayDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        [RequireAuth]
        public async Task<IActionResult> GetMe()
        {
            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var role = HttpContext.Session.GetString("Role");

            if (role == "Organiser")
            {
                var organiser = await _context.Organisers.FindAsync(userId);
                if (organiser == null) return NotFound();
                return Ok(new ProfileResponse(organiser.Id, organiser.FullName, organiser.Email, organiser.Phone, "Organiser", organiser.CreatedAt));
            }

            var participant = await _context.Participants.FindAsync(userId);
            if (participant == null) return NotFound();
            return Ok(new ProfileResponse(participant.Id, participant.FullName, participant.Email, participant.Phone, "Participant", participant.CreatedAt));
        }

        [HttpPut("me")]
        [RequireAuth]
        public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var role = HttpContext.Session.GetString("Role");

            // Same cross-table rule AuthController enforces at registration:
            // an email can't exist in BOTH tables, so changing to one that's
            // already taken anywhere else has to be blocked here too.
            bool emailTakenElsewhere = role == "Organiser"
                ? await _context.Participants.AnyAsync(p => p.Email == request.Email)
                  || await _context.Organisers.AnyAsync(o => o.Email == request.Email && o.Id != userId)
                : await _context.Organisers.AnyAsync(o => o.Email == request.Email)
                  || await _context.Participants.AnyAsync(p => p.Email == request.Email && p.Id != userId);

            if (emailTakenElsewhere)
                return Conflict(new { message = "Email already in use." });

            if (role == "Organiser")
            {
                var organiser = await _context.Organisers.FindAsync(userId);
                if (organiser == null) return NotFound();

                organiser.FullName = request.FullName;
                organiser.Phone = request.Phone;
                organiser.Email = request.Email;
                await _context.SaveChangesAsync();

                return Ok(new ProfileResponse(organiser.Id, organiser.FullName, organiser.Email, organiser.Phone, "Organiser", organiser.CreatedAt));
            }

            var participant = await _context.Participants.FindAsync(userId);
            if (participant == null) return NotFound();

            participant.FullName = request.FullName;
            participant.Phone = request.Phone;
            participant.Email = request.Email;
            await _context.SaveChangesAsync();

            return Ok(new ProfileResponse(participant.Id, participant.FullName, participant.Email, participant.Phone, "Participant", participant.CreatedAt));
        }
    }
}