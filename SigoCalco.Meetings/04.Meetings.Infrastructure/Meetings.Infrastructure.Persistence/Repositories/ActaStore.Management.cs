using System.Data;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed partial class ActaStore
{
    private IQueryable<ActaListItemDto> OwnedRows(int userId, int? id = null, int skip = 0, int take = int.MaxValue)
    {
        var rows = from a in db.Set<Acta>().AsNoTracking()
        join type in db.Set<ActaType>() on a.ActaTypeId equals type.ActaTypeId
        join status in db.Set<ActaStatus>() on a.ActaStatusId equals status.ActaStatusId
        join m in db.Set<ScheduledMeeting>() on a.ActaId equals m.ActaId into meetings
        from meeting in meetings.DefaultIfEmpty()
        where a.CreatedByUser == userId && (!id.HasValue || a.ActaId == id.Value)
        select new { a, type, status, meeting };
        // Apply filtering/ordering to mapped columns before projecting the immutable DTO constructor.
        return rows.OrderByDescending(r => r.a.ActaId).Skip(skip).Take(take).Select(r => new ActaListItemDto(
            r.a.ActaId, r.a.ActaCode, r.a.Title, r.a.DocumentDate, r.a.ActaTypeId, r.type.Name, r.a.ActaStatusId, r.status.Name,
            r.a.SolutionCenterCode, r.a.CreatedByUser, r.a.CreatedAt, r.a.UpdatedAt, r.meeting != null, r.meeting == null ? null : r.meeting.Modality));
    }

    public async Task<PagedActasDto> GetOwnedAsync(int userId, ActaQuery query, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(db.Database.IsMySql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, ct);
        var total = await db.Set<Acta>().CountAsync(a => a.CreatedByUser == userId, ct);
        var items = await OwnedRows(userId, skip: (query.Page - 1) * query.PageSize, take: query.PageSize).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public async Task<ActaDetailDto> GetOwnedDetailAsync(int id, int userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(db.Database.IsMySql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, ct);
        var acta = await db.Set<Acta>().AsNoTracking().SingleOrDefaultAsync(a => a.ActaId == id && a.CreatedByUser == userId, ct)
            ?? throw Missing();
        var row = await OwnedRows(userId, id).SingleAsync(ct);
        var source = (await FindVersionAsync(acta.TemplateVersionId, ct))!;
        var contents = await db.Set<ActaSection>().AsNoTracking().Where(s => s.ActaId == id).ToDictionaryAsync(s => s.TemplateSectionId, ct);
        var sections = source.Version.Sections.OrderBy(s => s.SortOrder).Select(s => new ActaSectionDto(
            s.TemplateSectionId, s.Title, s.ContentType, s.SortOrder, s.IsRequired, Json(s.ConfigurationJson),
            contents.TryGetValue(s.TemplateSectionId, out var content) ? Json(content.ContentJson) : null)).ToArray();
        var participants = await (from link in db.Set<ActaParticipant>().AsNoTracking()
                                  join person in db.Set<Participant>() on link.ParticipantId equals person.ParticipantId
                                  where link.ActaId == id orderby link.ActaParticipantId
                                  select new ActaParticipantDto(person.ParticipantId, person.UserName, person.Email, person.ParticipantType, person.UserLogin, link.Attended)).ToArrayAsync(ct);
        var meeting = await db.Set<ScheduledMeeting>().AsNoTracking().SingleOrDefaultAsync(m => m.ActaId == id, ct);
        ActaMeetingDto? meetingDto = null;
        if (meeting is not null)
        {
            var reservation = await db.Set<MeetingReservation>().AsNoTracking().SingleOrDefaultAsync(r => r.MeetingId == meeting.MeetingId, ct);
            meetingDto = new(meeting.MeetingId, meeting.Modality, DateTime.SpecifyKind(meeting.StartAtUtc, DateTimeKind.Utc),
                DateTime.SpecifyKind(meeting.EndAtUtc, DateTimeKind.Utc), meeting.TimeZone, meeting.LocationDescription, meeting.OrganizerUserId,
                reservation is null ? null : new(reservation.MeetingReservationId, reservation.ExternalRoomId, reservation.ExternalReservationId,
                    reservation.ReservationStatus, reservation.ExternalReservationId?.StartsWith("SIMULATED:", StringComparison.Ordinal) == true,
                    reservation.RequestedAt, reservation.SynchronizedAt));
        }
        await transaction.CommitAsync(ct);
        return new(row, source.Template.TemplateId, source.Template.Name, source.Version.TemplateVersionId, source.Version.VersionNumber, sections, participants, meetingDto);
    }

    public Task UpdateOwnedAsync(int id, int userId, string title, DateOnly date, IReadOnlyList<ActaSection> sections, CancellationToken ct) =>
        MutateOwnedAsync(id, userId, async (acta, status) =>
        {
            ActaLifecycle.RequireDraft(status);
            acta.Title = title; acta.DocumentDate = date;
            await db.Set<ActaSection>().Where(s => s.ActaId == id).ExecuteDeleteAsync(ct);
            foreach (var section in sections)
            {
                section.ActaSectionId = 0; section.ActaId = id;
                if (section.TemplateVersionId != acta.TemplateVersionId) throw MeetingsRequestException.Invalid("Debe conservarse la versión original.");
                db.Add(section);
            }
        }, ct);

    public Task ReplaceParticipantsAsync(int id, int userId, IReadOnlyList<Participant> participants, CancellationToken ct) =>
        MutateOwnedAsync(id, userId, async (_, status) =>
        {
            ActaLifecycle.RequireDraft(status);
            var existing = await db.Set<ActaParticipant>().Where(p => p.ActaId == id).ToListAsync(ct);
            var retained = new HashSet<int>();
            foreach (var participant in participants)
            {
                var person = await db.Set<Participant>().SingleOrDefaultAsync(p => p.Email == participant.Email, ct);
                if (person is null)
                {
                    participant.ParticipantId = 0; db.Add(participant); await db.SaveChangesAsync(ct); person = participant;
                }
                if (!retained.Add(person.ParticipantId)) throw MeetingsRequestException.Invalid("Los participantes están duplicados.");
                if (!existing.Any(p => p.ParticipantId == person.ParticipantId)) db.Add(new ActaParticipant { ActaId = id, ParticipantId = person.ParticipantId });
            }
            // Retained associations keep their attendance; only this acta's removed associations are deleted.
            db.RemoveRange(existing.Where(p => !retained.Contains(p.ParticipantId)));
        }, ct);

    public Task UpdateAttendanceAsync(int id, int userId, int participantId, bool? attended, CancellationToken ct) =>
        MutateOwnedAsync(id, userId, async (_, status) =>
        {
            ActaLifecycle.RequireDraft(status);
            var link = await db.Set<ActaParticipant>().SingleOrDefaultAsync(p => p.ActaId == id && p.ParticipantId == participantId, ct)
                ?? throw MeetingsRequestException.NotFound("El participante no está asociado al acta.");
            link.Attended = attended;
        }, ct);

    public Task ChangeStatusAsync(int id, int userId, string next, CancellationToken ct) =>
        MutateOwnedAsync(id, userId, async (acta, status) =>
        {
            ActaLifecycle.RequireTransition(status, next);
            acta.ActaStatusId = await db.Set<ActaStatus>().Where(s => s.Name == next).Select(s => s.ActaStatusId).SingleAsync(ct);
        }, ct);

    private async Task MutateOwnedAsync(int id, int userId, Func<Acta, string, Task> mutate, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(db.Database.IsMySql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable, ct);
            try
            {
                db.ChangeTracker.Clear();
                var query = db.Database.IsMySql()
                    ? db.Set<Acta>().FromSqlInterpolated($"SELECT * FROM sigocalco_meetings_acta WHERE acta_id={id} AND created_by_user={userId} FOR UPDATE")
                    : db.Set<Acta>().Where(a => a.ActaId == id && a.CreatedByUser == userId);
                var acta = (await query.ToListAsync(ct)).SingleOrDefault() ?? throw Missing();
                var status = await db.Set<ActaStatus>().Where(s => s.ActaStatusId == acta.ActaStatusId).Select(s => s.Name).SingleAsync(ct);
                await mutate(acta, status);
                acta.UpdatedByUser = userId; acta.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return;
            }
            catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 } && attempt < 2)
            { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); }
            catch (MySqlException ex) when (ex.Number is 1205 or 1213)
            { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw new MeetingsRequestException(409, "El acta está siendo modificada; vuelva a intentar."); }
            catch
            { await transaction.RollbackAsync(CancellationToken.None); db.ChangeTracker.Clear(); throw; }
        }
    }
    private static JsonElement? Json(string? value) => value is null ? null : JsonSerializer.Deserialize<JsonElement>(value);
    private static MeetingsRequestException Missing() => MeetingsRequestException.NotFound("No se encontró el acta.");
}
