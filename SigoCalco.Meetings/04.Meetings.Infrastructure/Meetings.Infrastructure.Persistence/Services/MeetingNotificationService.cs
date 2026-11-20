using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Services;

public sealed class MeetingNotificationService(MeetingsDbContext db, IMeetingNotificationSender sender) : IMeetingNotificationService
{
    public Task<NotificationSummaryDto> SendForActaAsync(int actaId, CancellationToken ct) =>
        SendAsync(db.Set<MeetingNotification>().Where(n => n.ActaId == actaId), ct);
    public Task<NotificationSummaryDto> SendForMeetingAsync(int meetingId, CancellationToken ct) =>
        SendAsync(db.Set<MeetingNotification>().Where(n => n.MeetingId == meetingId), ct);
    private async Task<NotificationSummaryDto> SendAsync(IQueryable<MeetingNotification> query, CancellationToken ct)
    {
        var ids = await query.AsNoTracking()
            .OrderBy(n => n.MeetingNotificationId).Select(n => n.MeetingNotificationId).ToArrayAsync(ct);
        var items = new List<NotificationItemDto>();
        foreach (var id in ids) items.Add(await RetryAsync(id, ct));
        var state = items.Count == 0 ? "SIN_DESTINATARIOS" : items.All(n => n.State == items[0].State) ? items[0].State : "PARCIAL";
        return new(state, items.Count > 0 && items.All(n => n.State == "SIMULADO"),
            items.Count > 0 && items.All(n => n.State == "ENVIADO"), items);
    }
    public async Task<NotificationItemDto> RetryAsync(long notificationId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("SMTP requiere la transacción local cerrada.");
        var notification = await FindAsync(notificationId, ct);
        // An interrupted PROCESANDO or uncertain SMTP response requires manual reconciliation, never automatic resend.
        if (notification.Status is not ("PENDIENTE" or "ERROR")) return Map(notification);
        var now = DateTime.UtcNow;
        var claimed = await db.Set<MeetingNotification>().Where(n => n.MeetingNotificationId == notificationId &&
                (n.Status == "PENDIENTE" || n.Status == "ERROR"))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, "PROCESANDO").SetProperty(n => n.Attempts, n => n.Attempts + 1)
                .SetProperty(n => n.LastError, (string?)null).SetProperty(n => n.UpdatedAt, now), ct);
        if (claimed != 1) return Map(await FindAsync(notificationId, ct));
        string state;
        string? error = null;
        DateTime? acceptedAt = null;
        try
        {
            var result = await sender.SendAsync(notification, ct);
            state = result.Simulated ? "SIMULADO" : "ENVIADO";
            if (!result.Simulated) acceptedAt = DateTime.UtcNow;
        }
        catch (NotificationFailure ex) { state = ex.Uncertain ? "INCIERTO" : "ERROR"; error = ex.Message; }
        catch { state = "INCIERTO"; error = "No se pudo determinar si SMTP aceptó el mensaje. Verifique antes de autorizar cualquier reenvío."; }
        now = DateTime.UtcNow;
        if (error?.Length > 500) error = error[..500];
        // A durable PROCESANDO state also prevents duplicates if this final write fails after SMTP acceptance.
        await db.Set<MeetingNotification>().Where(n => n.MeetingNotificationId == notificationId && n.Status == "PROCESANDO")
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, state).SetProperty(n => n.LastError, error)
                .SetProperty(n => n.AcceptedAt, acceptedAt).SetProperty(n => n.UpdatedAt, now), CancellationToken.None);
        return Map(await FindAsync(notificationId, CancellationToken.None));
    }
    private async Task<MeetingNotification> FindAsync(long id, CancellationToken ct) =>
        await db.Set<MeetingNotification>().AsNoTracking().SingleOrDefaultAsync(n => n.MeetingNotificationId == id, ct)
        ?? throw MeetingsRequestException.NotFound("No se encontró la notificación.");
    private static NotificationItemDto Map(MeetingNotification value) => new(value.MeetingNotificationId, value.Status, value.Attempts, value.LastError);
}
