namespace Meetings.Domain.Models.Actas;

// One durable notification per meeting/recipient. SMTP acceptance is not final delivery.
public sealed class MeetingNotification
{
    public long MeetingNotificationId { get; set; }
    public int MeetingId { get; set; }
    public string RecipientEmail { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string TextBody { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    public string Status { get; set; } = "PENDIENTE";
    public string? LastError { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
}
