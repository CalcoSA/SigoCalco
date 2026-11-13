using System.Data;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed class ActaStore(MeetingsDbContext db) : IActaStore
{
    public async Task<ActaTemplate?> FindVersionAsync(int versionId, CancellationToken ct)
    {
        var version = await db.Set<TemplateVersion>().AsNoTracking().Include(v => v.Sections)
            .SingleOrDefaultAsync(v => v.TemplateVersionId == versionId, ct);
        if (version is null) return null;
        var template = await db.Set<Template>().AsNoTracking().Include(t => t.ActaType)
            .SingleAsync(t => t.TemplateId == version.TemplateId, ct);
        return new(template, version);
    }

    public async Task<CreatedActaDto> CreateAsync(NewActa input, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            if (await db.Set<Acta>().AnyAsync(a => a.ActaCode == input.Acta.ActaCode, ct)) throw Duplicate();
            await using var transaction = await db.Database.BeginTransactionAsync(
                db.Database.IsMySql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
            try
            {
                input.Acta.ActaId = 0;
                input.Acta.ActaStatusId = await db.Set<ActaStatus>().Where(s => s.Name == "BORRADOR").Select(s => s.ActaStatusId).SingleAsync(ct);
                db.Add(input.Acta);
                await db.SaveChangesAsync(ct);
                foreach (var section in input.Sections)
                {
                    section.ActaSectionId = 0;
                    section.ActaId = input.Acta.ActaId;
                    db.Add(section);
                }
                foreach (var participant in input.Participants)
                {
                    var existing = await db.Set<Participant>().SingleOrDefaultAsync(p => p.Email == participant.Email, ct);
                    if (existing is null)
                    {
                        participant.ParticipantId = 0;
                        db.Add(participant);
                        await db.SaveChangesAsync(ct);
                        existing = participant;
                    }
                    // Reusing a participant never overwrites its historical identity from a request.
                    db.Add(new ActaParticipant { ActaId = input.Acta.ActaId, ParticipantId = existing.ParticipantId });
                }
                if (input.Meeting is { } meeting)
                {
                    meeting.MeetingId = 0;
                    meeting.ActaId = input.Acta.ActaId;
                    meeting.MeetingStatusId = await db.Set<MeetingStatus>().Where(s => s.Name == "PROGRAMADA")
                        .Select(s => s.MeetingStatusId).SingleAsync(ct);
                    db.Add(meeting);
                    await db.SaveChangesAsync(ct);
                    if (meeting.Modality == "PRESENCIAL") db.Add(new MeetingReservation
                    {
                        MeetingId = meeting.MeetingId, ExternalRoomId = input.RoomId, RequestedAt = DateTime.UtcNow
                    });
                    else db.Add(new MeetingCalendar { MeetingId = meeting.MeetingId, OrganizerEmail = input.OrganizerEmail!, CreateTeams = true });
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new(input.Acta.ActaId, input.Acta.ActaCode, input.Meeting?.MeetingId, input.Meeting?.Modality,
                    input.Meeting is null ? null : new(input.Meeting.Modality == "VIRTUAL" ? "MICROSOFT_GRAPH" : "RESERVATIONS", "PENDIENTE", false));
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await transaction.DisposeAsync();
                db.ChangeTracker.Clear();
                if (await db.Set<Acta>().AnyAsync(a => a.ActaCode == input.Acta.ActaCode, ct)) throw Duplicate();
                // Another request may have inserted the same participant email. Re-read under a new transaction.
                if (attempt >= 2) throw new MeetingsRequestException(409, "Conflicto concurrente. Reintente con la misma Idempotency-Key.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }
    }
    private static MeetingsRequestException Duplicate() => new(409,
        "La Idempotency-Key ya fue utilizada. El acta existente no se modifica ni se repite la integración.");
}
