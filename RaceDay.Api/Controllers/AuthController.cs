using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly IPasswordHasher<object> _passwordHasher;

        public AuthController(RaceDayDbContext context, IPasswordHasher<object> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        /// <summary>
        /// Registers a new Organiser account.
        /// </summary>
        /// <param name="request">Full name, email, and password for the new account.</param>
        /// <response code="201">Organiser created successfully.</response>
        /// <response code="400">The request body failed validation (missing field, invalid email, or password under 8 characters).</response>
        /// <response code="409">An account already exists with this email, under either role.</response>
        [HttpPost("register/organiser")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterOrganiser(RegisterRequest request)
        {
            if (await EmailExistsAsync(request.Email))
                return Conflict(new { message = "Email already registered." });

            var organiser = new Organiser
            {
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = _passwordHasher.HashPassword(null!, request.Password)
            };

            _context.Organisers.Add(organiser);
            await _context.SaveChangesAsync();

            return StatusCode(201, new AuthResponse(organiser.Id, organiser.FullName, organiser.Email, "Organiser"));
        }

        /// <summary>
        /// Registers a new Participant account.
        /// </summary>
        /// <param name="request">Full name, email, and password for the new account.</param>
        /// <response code="201">Participant created successfully.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="409">An account already exists with this email, under either role.</response>
        [HttpPost("register/participant")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterParticipant(RegisterRequest request)
        {
            if (await EmailExistsAsync(request.Email))
                return Conflict(new { message = "Email already registered." });

            var participant = new Participant
            {
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = _passwordHasher.HashPassword(null!, request.Password)
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            return StatusCode(201, new AuthResponse(participant.Id, participant.FullName, participant.Email, "Participant"));
        }

        /// <summary>
        /// Authenticates a user by checking both the Organisers and Participants tables for a
        /// matching email, then stores the user's id and role in the session for subsequent requests.
        /// </summary>
        /// <param name="request">Email and password.</param>
        /// <response code="200">Login successful. The session now holds this user's id and role.</response>
        /// <response code="400">The request body failed validation.</response>
        /// <response code="401">Email not found, or password incorrect.</response>
        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var organiser = await _context.Organisers.FirstOrDefaultAsync(o => o.Email == request.Email);
            if (organiser != null && VerifyPassword(organiser.PasswordHash, request.Password))
            {
                HttpContext.Session.SetInt32("UserId", organiser.Id);
                HttpContext.Session.SetString("Role", "Organiser");
                return Ok(new AuthResponse(organiser.Id, organiser.FullName, organiser.Email, "Organiser"));
            }

            var participant = await _context.Participants.FirstOrDefaultAsync(p => p.Email == request.Email);
            if (participant != null && VerifyPassword(participant.PasswordHash, request.Password))
            {
                HttpContext.Session.SetInt32("UserId", participant.Id);
                HttpContext.Session.SetString("Role", "Participant");
                return Ok(new AuthResponse(participant.Id, participant.FullName, participant.Email, "Participant"));
            }

            return Unauthorized(new { message = "Invalid email or password." });
        }

        private async Task<bool> EmailExistsAsync(string email)
        {
            var inOrganisers = await _context.Organisers.AnyAsync(o => o.Email == email);
            var inParticipants = await _context.Participants.AnyAsync(p => p.Email == email);
            return inOrganisers || inParticipants;
        }

        private bool VerifyPassword(string storedHash, string providedPassword)
        {
            var result = _passwordHasher.VerifyHashedPassword(null!, storedHash, providedPassword);
            return result == PasswordVerificationResult.Success;
        }
    }
}