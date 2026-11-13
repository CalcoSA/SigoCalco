using Meetings.Application.Interfaces;

namespace Meetings.Infrastructure.Persistence.Services;

public sealed class SimulatedRoomReservationProvider : IRoomReservationProvider
{
    public Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult($"SIMULATED:{request.IdempotencyKey}", true));
}
public sealed class SimulatedCalendarProvider : IMeetingCalendarProvider
{
    public Task<ProviderResult> SynchronizeAsync(MeetingIntegrationRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult($"SIMULATED:{request.IdempotencyKey}", true));
}
