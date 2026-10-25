using System.ComponentModel.DataAnnotations;
 
namespace RaceDay.Api.DTOs
{
    public record CreateEventRequest(
        [Required, MaxLength(150)] string Name,
        string? Description,
        [Required] DateTime EventDate,
        [Required, MaxLength(150)] string Location,
        [Required, Range(0.01, 9999.99)] decimal DistanceKm,
        [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk, or Cycle.")] string EventType
    );
 
    public record UpdateEventRequest(
        [Required, MaxLength(150)] string Name,
        string? Description,
        [Required] DateTime EventDate,
        [Required, MaxLength(150)] string Location,
        [Required, Range(0.01, 9999.99)] decimal DistanceKm,
        [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk, or Cycle.")] string EventType
    );
 
    public record EventResponse(
        int Id, int OrganiserId, string Name, string? Description,
        DateTime EventDate, string Location, decimal DistanceKm,
        string EventType, DateTime CreatedAt
    );
 
    // Part 3: one row of the Organiser's own events, with how many active
    // (non-cancelled) enrolments the event has. Powers the dashboard and events page.
    public record OrganiserEventSummary(
        int Id, string Name, string? Description, DateTime EventDate,
        string Location, decimal DistanceKm, string EventType,
        DateTime CreatedAt, int EnrolmentCount
    );
}