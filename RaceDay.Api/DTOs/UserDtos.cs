using System.ComponentModel.DataAnnotations;
 
namespace RaceDay.Api.DTOs
{
    public record UpdateProfileRequest(
        [Required, MaxLength(100)] string FullName,
        [MaxLength(20)] string? Phone,
        [Required, EmailAddress] string Email
    );
 
    // Part 3: ProfileImageUrl is the public Azure Blob Storage link, or null if no picture was uploaded
    public record ProfileResponse(
        int Id, string FullName, string Email, string? Phone,
        string? ProfileImageUrl, string Role, DateTime CreatedAt
    );
}