
namespace Meetings.Domain.Models;

public class Meeting
{
    public long MeetingId { get; set; }

    public long MinuteId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public string Modality { get; set; } = string.Empty;

    public long? ReservationId { get; set; }

    public string Status { get; set; } = "SCHEDULED";

    public long CreatedByUserId { get; set; }

    public string? OutlookEventId { get; set; }

    public string? OutlookCalendarId { get; set; }

    public string OutlookSyncStatus { get; set; } = "PENDING";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Relación obligatoria con el acta
    public Minute Minute { get; set; } = null!;
}
