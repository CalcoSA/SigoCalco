using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Interfaces;

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "notificaciones@crepesywafflesantioquia.com";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "notificaciones@crepesywafflesantioquia.com";
    public string FromName { get; set; } = "SigoCalco - Actas y Reuniones";
    public string ReplyTo { get; set; } = "";
    public bool UseStartTls { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 15;
}
public sealed record NotificationReceipt(bool Simulated);
public sealed record NotificationItemDto(long NotificationId, string State, int Attempts, string? Error);
public sealed record NotificationSummaryDto(string State, bool Simulated, bool SmtpAccepted,
    IReadOnlyList<NotificationItemDto> Items);
public interface IMeetingNotificationSender
{
    Task<NotificationReceipt> SendAsync(MeetingNotification notification, CancellationToken ct);
}
public interface IMeetingNotificationService
{
    Task<NotificationSummaryDto> SendForMeetingAsync(int meetingId, CancellationToken ct);
    Task<NotificationItemDto> RetryAsync(long notificationId, CancellationToken ct);
}
public sealed class NotificationFailure(string message, bool uncertain = false) : Exception(message)
{
    public bool Uncertain { get; } = uncertain;
}
