using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record RegisterRequest(
        [Required] string FullName,
        [Required, EmailAddress] string Email,
        [Required, MinLength(8)] string Password
    );

    public record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required] string Password
    );

    public record AuthResponse(int Id, string FullName, string Email, string Role);
}