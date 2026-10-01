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

    public record ResultResponse(
        int Id, int EnrolmentId, int ParticipantId, string ParticipantName,
        int EventId, string EventName, TimeSpan FinishTime, int Position, DateTime CreatedAt
    );
}