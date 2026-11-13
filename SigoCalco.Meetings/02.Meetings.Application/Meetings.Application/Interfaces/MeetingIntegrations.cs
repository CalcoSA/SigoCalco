using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public sealed class CalendarOptions
{
    public string Mode { get; set; } = "Simulated";
    public string OrganizerEmail { get; set; } = "simulated@example.invalid";
    public bool AllowExternalInvitations { get; set; }
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
}
public sealed record IntegrationAttendee(string Name, string Email);
public sealed record MeetingIntegrationRequest(int MeetingId, string IdempotencyKey, string Title,
    DateTime StartAtUtc, DateTime EndAtUtc, string TimeZone, string? Location, string? RoomId,
    string? OrganizerEmail, IReadOnlyList<IntegrationAttendee> Attendees, string? ExternalId);
public sealed record ProviderResult(string? ExternalId, bool Simulated, string? TeamsJoinUrl = null, bool Confirmed = false);
public interface IRoomReservationProvider
{
    Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct);
}
public interface IMeetingCalendarProvider
{
    Task<ProviderResult> SynchronizeAsync(MeetingIntegrationRequest request, CancellationToken ct);
}
public interface IMeetingIntegrationService
{
    Task<IntegrationDto> SynchronizeAsync(int meetingId, CancellationToken ct);
}
// The message contains only a safe operator-facing description, never provider response bodies or credentials.
public sealed class IntegrationFailure(string message) : Exception(message);
