using Microsoft.AspNetCore.Http;
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

        /// <summary>Retrieves the logged-in user's own profile, resolved server-side to the Organiser or Participant record matching the current session.</summary>
        /// <response code="200">Profile details.</response>
        /// <response code="401">No active session.</response>
        [HttpGet("me")]
        [RequireAuth]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

        /// <summary>Updates the logged-in user's own profile information, in whichever table the current session resolves to.</summary>
        /// <param name="request">Updated full name, phone, and email.</param>
        /// <response code="200">Profile updated successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">No active session.</response>
        /// <response code="409">This email is already in use by another account.</response>
        [HttpPut("me")]
        [RequireAuth]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateMe(UpdateProfileRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var role = HttpContext.Session.GetString("Role");

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