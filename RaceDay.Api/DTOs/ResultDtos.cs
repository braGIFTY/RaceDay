using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record CreateResultRequest(
        [Required] TimeSpan FinishTime,
        [Required, Range(1, int.MaxValue)] int Position
    );

    public record UpdateResultRequest(
        [Required] TimeSpan FinishTime,
        [Required, Range(1, int.MaxValue)] int Position
    );

    // Part 3: now also carries the event date, the category, and how many finishers
    // have a recorded result for the event, so a client can show "47th out of 312".
    public record ResultResponse(
        int Id, int EnrolmentId, int ParticipantId, string ParticipantName,
        int EventId, string EventName, DateTime EventDate, string CategoryName,
        TimeSpan FinishTime, int Position, int TotalFinishers, DateTime CreatedAt
    );
}