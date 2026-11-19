using Meetings.Application.Interfaces;

namespace Meetings.Infrastructure.Persistence.Services;

public sealed class SimulatedRoomReservationProvider : IRoomReservationProvider
{
    public Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct) =>
        Task.FromResult(new ProviderResult($"SIMULATED:{request.IdempotencyKey}", true));
}
public sealed class SimulatedNotificationSender : IMeetingNotificationSender
{
    public Task<NotificationReceipt> SendAsync(Meetings.Domain.Models.Actas.MeetingNotification notification, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new NotificationReceipt(true));
    }
}
