using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.DTOs
{
    public record CreateEnrolmentRequest(
        [Required] int EventId,
        [Required] int CategoryId
    );

    public record EnrolmentResponse(
        int Id, int ParticipantId, string ParticipantName,
        int EventId, string EventName,
        int CategoryId, string CategoryName,
        DateTime EnrolmentDate, string Status
    );
}