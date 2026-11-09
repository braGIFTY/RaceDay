namespace RaceDay.Mvc.Models
{
    // The website's own description of what the API sends back. It deliberately does not
    // reference the API project: the two only ever meet over HTTP and JSON.

    public record AuthResponseDto(int Id, string FullName, string Email, string Role);

    public record EventDto(
        int Id, int OrganiserId, string Name, string? Description,
        DateTime EventDate, string Location, decimal DistanceKm,
        string EventType, string? BannerImageUrl, DateTime CreatedAt);

    public record OrganiserEventSummaryDto(
        int Id, string Name, string? Description, DateTime EventDate,
        string Location, decimal DistanceKm, string EventType,
        string? BannerImageUrl, DateTime CreatedAt, int EnrolmentCount);

    public record CategoryDto(int Id, int EventId, string Name, string? Description);

    public record EnrolmentDto(
        int Id, int ParticipantId, string ParticipantName,
        int EventId, string EventName, DateTime EventDate,
        int CategoryId, string CategoryName,
        DateTime EnrolmentDate, string Status);

    public record ResultDto(
        int Id, int EnrolmentId, int ParticipantId, string ParticipantName,
        int EventId, string EventName, DateTime EventDate, string CategoryName,
        TimeSpan FinishTime, int Position, int TotalFinishers, DateTime CreatedAt);

    public record ProfileDto(
        int Id, string FullName, string Email, string? Phone,
        string? ProfileImageUrl, string Role, DateTime CreatedAt);
}
