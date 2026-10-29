using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Filters;
using RaceDay.Api.Services;
 
namespace RaceDay.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly RaceDayDbContext _context;
        private readonly ImageUploader _imageUploader;
 
        public UsersController(RaceDayDbContext context, ImageUploader imageUploader)
        {
            _context = context;
            _imageUploader = imageUploader;
        }
 
        /// <summary>Retrieves the logged-in user's own profile, resolved server-side to the Organiser or Participant record matching the current session.</summary>
        /// <response code="200">Profile details, including the profile picture URL if one was uploaded.</response>
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
                return Ok(new ProfileResponse(organiser.Id, organiser.FullName, organiser.Email, organiser.Phone,
                    organiser.ProfileImageUrl, "Organiser", organiser.CreatedAt));
            }
 
            var participant = await _context.Participants.FindAsync(userId);
            if (participant == null) return NotFound();
            return Ok(new ProfileResponse(participant.Id, participant.FullName, participant.Email, participant.Phone,
                participant.ProfileImageUrl, "Participant", participant.CreatedAt));
        }
 
        /// <summary>Updates the logged-in user's own profile information, in whichever table the current session resolves to. The profile picture is changed separately, through the profile-image endpoint.</summary>
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
 
                return Ok(new ProfileResponse(organiser.Id, organiser.FullName, organiser.Email, organiser.Phone,
                    organiser.ProfileImageUrl, "Organiser", organiser.CreatedAt));
            }
 
            var participant = await _context.Participants.FindAsync(userId);
            if (participant == null) return NotFound();
 
            participant.FullName = request.FullName;
            participant.Phone = request.Phone;
            participant.Email = request.Email;
            await _context.SaveChangesAsync();
 
            return Ok(new ProfileResponse(participant.Id, participant.FullName, participant.Email, participant.Phone,
                participant.ProfileImageUrl, "Participant", participant.CreatedAt));
        }
 
        /// <summary>(Part 3 addition) Uploads or replaces the logged-in user's profile picture. The image is stored in Azure Blob Storage and its public URL is saved on the profile.</summary>
        /// <param name="file">The image: JPEG, PNG or WebP, up to 5 MB.</param>
        /// <response code="200">Picture saved. Returns the updated profile, including the image URL.</response>
        /// <response code="400">No file, a file over 5 MB, or a file that is not a JPEG, PNG or WebP image.</response>
        /// <response code="401">No active session.</response>
        /// <response code="503">Image storage is not set up, or could not be reached.</response>
        [HttpPost("me/profile-image")]
        [RequireAuth]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> UploadProfileImage(IFormFile? file)
        {
            var userId = HttpContext.Session.GetInt32("UserId")!.Value;
            var role = HttpContext.Session.GetString("Role");
 
            var upload = await _imageUploader.UploadAsync(file, "profiles");
            if (!upload.Succeeded)
                return StatusCode(upload.StatusCode, new { message = upload.Error });
 
            if (role == "Organiser")
            {
                var organiser = await _context.Organisers.FindAsync(userId);
                if (organiser == null) return NotFound();
 
                var previousOrganiserImage = organiser.ProfileImageUrl;
                organiser.ProfileImageUrl = upload.Url;
                await _context.SaveChangesAsync();
                await _imageUploader.DeleteAsync(previousOrganiserImage);
 
                return Ok(new ProfileResponse(organiser.Id, organiser.FullName, organiser.Email, organiser.Phone,
                    organiser.ProfileImageUrl, "Organiser", organiser.CreatedAt));
            }
 
            var participant = await _context.Participants.FindAsync(userId);
            if (participant == null) return NotFound();
 
            var previousParticipantImage = participant.ProfileImageUrl;
            participant.ProfileImageUrl = upload.Url;
            await _context.SaveChangesAsync();
            await _imageUploader.DeleteAsync(previousParticipantImage);
 
            return Ok(new ProfileResponse(participant.Id, participant.FullName, participant.Email, participant.Phone,
                participant.ProfileImageUrl, "Participant", participant.CreatedAt));
        }
    }
}