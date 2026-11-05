namespace RaceDay.Mvc.Models
{
    // The website's own description of what the API sends back. It deliberately does not
    // reference the API project: the two only ever meet over HTTP and JSON.
 
    public record AuthResponseDto(int Id, string FullName, string Email, string Role);
 
    public record EventDto(
        int Id, int OrganiserId, string Name, string? Description,
        DateTime EventDate, string Location, decimal DistanceKm,
        string EventType, string? BannerImageUrl, DateTime CreatedAt);
}
 