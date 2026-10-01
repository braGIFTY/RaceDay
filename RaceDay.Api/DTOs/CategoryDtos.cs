using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record CreateCategoryRequest(
        [Required, MaxLength(50)] string Name,
        [MaxLength(255)] string? Description
    );

    public record UpdateCategoryRequest(
        [Required, MaxLength(50)] string Name,
        [MaxLength(255)] string? Description
    );

    public record CategoryResponse(int Id, int EventId, string Name, string? Description);
}
