using System.Data;
using System.Security.Cryptography;
using System.Text;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Meetings.Infrastructure.Persistence.Services;

public sealed class MeetingIntegrationService(MeetingsDbContext db, IRoomReservationProvider reservations,
    IMeetingCalendarProvider calendars, ILogger<MeetingIntegrationService> logger) : IMeetingIntegrationService
{
    // Bounded stripes are used only by the SQLite test provider. MySQL locks coordinate multiple app instances.
    private static readonly SemaphoreSlim[] TestLocks = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<IntegrationDto> SynchronizeAsync(int meetingId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("La integración requiere una transacción local cerrada.");
        await using var lease = await AcquireAsync(meetingId, ct);
        db.ChangeTracker.Clear();
        var meeting = await db.Set<ScheduledMeeting>().SingleOrDefaultAsync(m => m.MeetingId == meetingId, ct)
            ?? throw MeetingsRequestException.NotFound("No se encontró la reunión.");
        var acta = await db.Set<Acta>().SingleAsync(a => a.ActaId == meeting.ActaId, ct);
        var reservation = await db.Set<MeetingReservation>().SingleOrDefaultAsync(r => r.MeetingId == meetingId, ct);
        var calendar = await db.Set<MeetingCalendar>().SingleOrDefaultAsync(c => c.MeetingId == meetingId, ct);
        if (reservation is null && calendar is null) throw new InvalidOperationException("Falta el registro de integración.");
        var cancelled = await db.Set<MeetingStatus>().AnyAsync(s => s.MeetingStatusId == meeting.MeetingStatusId && s.Name == "CANCELADA", ct);
        if (cancelled || reservation?.ReservationStatus is "CONFIRMADA" or "CANCELADA" || calendar?.SyncStatus is "SINCRONIZADO" or "CANCELADO" ||
            IsSimulation(reservation?.ExternalReservationId ?? calendar?.ExternalEventId))
            return Map(reservation, calendar, cancelled);
        var attendees = await (from relation in db.Set<ActaParticipant>()
            join person in db.Set<Participant>() on relation.ParticipantId equals person.ParticipantId
            where relation.ActaId == acta.ActaId
            select new IntegrationAttendee(person.UserName, person.Email)).ToArrayAsync(ct);
        var request = new MeetingIntegrationRequest(meetingId, acta.ActaCode, acta.Title,
            meeting.StartAtUtc, meeting.EndAtUtc, meeting.TimeZone, meeting.LocationDescription,
            reservation?.ExternalRoomId, calendar?.OrganizerEmail, attendees, reservation?.ExternalReservationId ?? calendar?.ExternalEventId);
        try
        {
            var result = reservation is not null ? await reservations.ReserveAsync(request, ct) : await calendars.SynchronizeAsync(request, ct);
            if (reservation is not null)
            {
                reservation.ExternalReservationId = result.ExternalId;
                reservation.ReservationStatus = result.Simulated || !result.Confirmed ? "SOLICITADA" : "CONFIRMADA";
                reservation.SynchronizedAt = result.Simulated || !result.Confirmed ? null : DateTime.UtcNow;
            }
            else
            {
                calendar!.ExternalEventId = result.ExternalId;
                calendar.TeamsJoinUrl = result.Simulated ? null : result.TeamsJoinUrl;
                calendar.SyncStatus = !result.Simulated && result.TeamsJoinUrl is not null ? "SINCRONIZADO" : "PENDIENTE";
                calendar.SynchronizedAt = calendar.SyncStatus == "SINCRONIZADO" ? DateTime.UtcNow : null;
                calendar.LastError = !result.Simulated && result.TeamsJoinUrl is null
                    ? "Evento creado; el enlace de Teams aún no está disponible. Reintente para consultar el mismo evento." : null;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var error = ex is IntegrationFailure ? ex.Message : "El proveedor no respondió correctamente. Revise la configuración y reintente de forma controlada.";
            logger.LogWarning("Integration failed for meeting {MeetingId} ({ExceptionType}).", meetingId, ex.GetType().Name);
            if (reservation is not null) reservation.ReservationStatus = "ERROR";
            else { calendar!.SyncStatus = "ERROR"; calendar.LastError = error; }
            db.Add(new MeetingsLog { Action = "INTEGRATION_ERROR", Module = "Meetings", UserName = "system",
                Description = $"meetingId={meetingId}: {error}"[..Math.Min(500, $"meetingId={meetingId}: {error}".Length)], CreatedAt = DateTime.UtcNow });
        }
        meeting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(reservation, calendar);
    }

    private static bool IsSimulation(string? id) => id?.StartsWith("SIMULATED:", StringComparison.Ordinal) == true;
    private static IntegrationDto Map(MeetingReservation? reservation, MeetingCalendar? calendar, bool cancelled = false)
    {
        var id = reservation?.ExternalReservationId ?? calendar?.ExternalEventId;
        var simulated = IsSimulation(id);
        return new(reservation is not null ? "RESERVATIONS" : "MICROSOFT_GRAPH",
            cancelled ? "CANCELADO" : simulated ? "SIMULADO" : reservation?.ReservationStatus ?? calendar!.SyncStatus,
            simulated, id, calendar?.SyncStatus == "SINCRONIZADO" && !cancelled ? calendar.TeamsJoinUrl : null,
            calendar?.LastError ?? (reservation?.ReservationStatus == "ERROR" ? "La reserva falló; consulte el registro de integración y reintente." : null));
    }

    private async Task<IAsyncDisposable> AcquireAsync(int meetingId, CancellationToken ct)
    {
        if (!db.Database.IsMySql())
        {
            var semaphore = TestLocks[(uint)meetingId % TestLocks.Length];
            await semaphore.WaitAsync(ct);
            return new Lease(() => { semaphore.Release(); return Task.CompletedTask; });
        }
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync(ct);
        var name = "meetings:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{connection.Database}:{meetingId}")))[..48];
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT GET_LOCK(@name, 5)";
            var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name; command.Parameters.Add(parameter);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) != 1)
                throw new MeetingsRequestException(409, "La reunión está siendo sincronizada. Espere y consulte antes de reintentar.");
        }
        catch { if (opened) await db.Database.CloseConnectionAsync(); throw; }
        return new Lease(async () =>
        {
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT RELEASE_LOCK(@name)";
                var parameter = command.CreateParameter(); parameter.ParameterName = "@name"; parameter.Value = name; command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync();
            }
            finally { if (opened) await db.Database.CloseConnectionAsync(); }
        });
    }
    private sealed class Lease(Func<Task> release) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await release();
    }
}
