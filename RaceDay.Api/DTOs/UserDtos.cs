using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record UpdateProfileRequest(
        [Required, MaxLength(100)] string FullName,
        [MaxLength(20)] string? Phone,
        [Required, EmailAddress] string Email
    );

    public record ProfileResponse(int Id, string FullName, string Email, string? Phone, string Role, DateTime CreatedAt);
}