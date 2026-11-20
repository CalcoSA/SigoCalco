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
    ILogger<MeetingIntegrationService> logger) : IMeetingIntegrationService
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
        if (meeting.Modality != "PRESENCIAL") return new("RESERVATIONS", "NO_APLICA", false);
        var acta = await db.Set<Acta>().SingleAsync(a => a.ActaId == meeting.ActaId, ct);
        var reservation = await db.Set<MeetingReservation>().SingleOrDefaultAsync(r => r.MeetingId == meetingId, ct);
        if (reservation is null) return new("RESERVATIONS", "NO_APLICA", false);
        var cancelled = await db.Set<MeetingStatus>().AnyAsync(s => s.MeetingStatusId == meeting.MeetingStatusId && s.Name == "CANCELADA", ct);
        if (cancelled || reservation.ReservationStatus is "CONFIRMADA" or "CANCELADA" || IsSimulation(reservation.ExternalReservationId))
            return Map(reservation, cancelled);
        var request = new MeetingIntegrationRequest(meetingId, acta.ActaCode, acta.Title,
            meeting.StartAtUtc, meeting.EndAtUtc, meeting.TimeZone, meeting.LocationDescription,
            reservation.ExternalRoomId, reservation.ExternalReservationId);
        try
        {
            var result = await reservations.ReserveAsync(request, ct);
            reservation.ExternalReservationId = result.ExternalId;
            reservation.ReservationStatus = result.Simulated || !result.Confirmed ? "SOLICITADA" : "CONFIRMADA";
            reservation.SynchronizedAt = result.Simulated || !result.Confirmed ? null : DateTime.UtcNow;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var error = ex is IntegrationFailure ? ex.Message : "El proveedor no respondió correctamente. Revise la configuración y reintente de forma controlada.";
            logger.LogWarning("Integration failed for meeting {MeetingId} ({ExceptionType}).", meetingId, ex.GetType().Name);
            reservation.ReservationStatus = "ERROR";
            db.Add(new MeetingsLog { Action = "INTEGRATION_ERROR", Module = "Meetings", UserName = "system",
                Description = $"meetingId={meetingId}: {error}"[..Math.Min(500, $"meetingId={meetingId}: {error}".Length)], CreatedAt = DateTime.UtcNow });
        }
        meeting.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Map(reservation);
    }

    private static bool IsSimulation(string? id) => id?.StartsWith("SIMULATED:", StringComparison.Ordinal) == true;
    private static IntegrationDto Map(MeetingReservation reservation, bool cancelled = false)
    {
        var id = reservation.ExternalReservationId;
        var simulated = IsSimulation(id);
        return new("RESERVATIONS", cancelled ? "CANCELADO" : simulated ? "SIMULADO" : reservation.ReservationStatus,
            simulated, id, reservation.ReservationStatus == "ERROR" ? "La reserva falló; consulte el registro de integración y reintente." : null);
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
