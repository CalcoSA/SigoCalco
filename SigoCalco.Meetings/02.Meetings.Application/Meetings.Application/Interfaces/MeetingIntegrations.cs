using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public sealed record MeetingIntegrationRequest(int MeetingId, string IdempotencyKey, string Title,
    DateTime StartAtUtc, DateTime EndAtUtc, string TimeZone, string? Location, string? RoomId,
    string? ExternalId);
public sealed record ProviderResult(string? ExternalId, bool Simulated, bool Confirmed = false);
public interface IRoomReservationProvider
{
    Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct);
}
public interface IMeetingIntegrationService
{
    Task<IntegrationDto> SynchronizeAsync(int meetingId, CancellationToken ct);
}
// The message contains only a safe operator-facing description, never provider response bodies or credentials.
public sealed class IntegrationFailure(string message) : Exception(message);
