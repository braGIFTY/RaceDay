using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record CreateEnrolmentRequest(
        [Required] int EventId,
        [Required] int CategoryId
    );

    // Part 3: now also carries the event date, so "my events" can show when each race is.
    public record EnrolmentResponse(
        int Id, int ParticipantId, string ParticipantName,
        int EventId, string EventName, DateTime EventDate,
        int CategoryId, string CategoryName,
        DateTime EnrolmentDate, string Status
    );
}