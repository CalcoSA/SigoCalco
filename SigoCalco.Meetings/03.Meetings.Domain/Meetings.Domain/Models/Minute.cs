
namespace Meetings.Domain.Models;

public class Minute
{
    public long MinuteId { get; set; }

    public long? MinuteTypeId { get; set; }

    public long? TemplateId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateOnly? OccurredOn { get; set; }

    public TimeOnly? ActualStartTime { get; set; }

    public TimeOnly? ActualEndTime { get; set; }

    public string? Modality { get; set; }

    public string? LocationDescription { get; set; }

    public string? MeetingKind { get; set; }

    public string Status { get; set; } = "DRAFT";

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? ClosedAt { get; set; }
}
