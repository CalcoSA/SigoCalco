namespace Meetings.Domain.Models;

public class Commitment
{
    public long CommitmentId { get; set; }

    public long MinuteId { get; set; }

    public string Description { get; set; } = string.Empty;

    public long? ResponsibleAttendeeId { get; set; }

    public DateOnly? DueDate { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}