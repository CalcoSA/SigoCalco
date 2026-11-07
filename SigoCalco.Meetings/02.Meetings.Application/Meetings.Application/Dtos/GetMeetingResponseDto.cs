
namespace Meetings.Application.Dtos;

public class GetMeetingResponseDto
{
    public long MeetingId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string Modality { get; set; } = string.Empty;

    public long? ReservationId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string OutlookSyncStatus { get; set; } = string.Empty;

    public MinuteSummaryDto Minute { get; set; } = null!;
}

public class MinuteSummaryDto
{
    public long MinuteId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateOnly? OccurredOn { get; set; }

    public string? MeetingKind { get; set; }
}
