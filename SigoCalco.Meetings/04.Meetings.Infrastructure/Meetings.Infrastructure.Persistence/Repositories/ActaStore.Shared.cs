using System.Data;
using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed partial class ActaStore
{
    private IQueryable<int> ParticipatedActaIds(string userLogin)
    {
        var login = ParticipantIdentity.NormalizeLogin(userLogin)
            ?? throw new ArgumentException("Se requiere una identidad validada.", nameof(userLogin));
        var people = db.Set<Participant>().Where(p => p.ParticipantType == "INTERNO" && p.UserLogin != null);
        // Explicit equality after trim/lower. MySQL's accent-insensitive default collation must
        // not conflate distinct logins that the validated identity compares ordinally.
        people = db.Database.IsMySql()
            ? people.Where(p => EF.Functions.Collate(p.UserLogin!.Trim().ToLower(), "utf8mb4_bin") == login)
            : people.Where(p => p.UserLogin!.Trim().ToLower() == login);
        return from link in db.Set<ActaParticipant>()
               join person in people on link.ParticipantId equals person.ParticipantId
               select link.ActaId;
    }

    public async Task<PagedActasDto> GetSharedAsync(int userId, string userLogin, ActaQuery query, CancellationToken ct)
    {
        var participated = ParticipatedActaIds(userLogin);
        // IN/EXISTS selects each acta once, even when multiple participant records match.
        var actas = db.Set<Acta>().Where(a => a.CreatedByUser != userId && participated.Contains(a.ActaId));
        await using var transaction = await db.Database.BeginTransactionAsync(db.Database.IsMySql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable, ct);
        var total = await actas.CountAsync(ct);
        var items = await ActaRows(actas, (query.Page - 1) * query.PageSize, query.PageSize, byCreation: true).ToArrayAsync(ct);
        await transaction.CommitAsync(ct);
        return new(items, query.Page, query.PageSize, total);
    }

    public Task<ActaDetailDto> GetReadableDetailAsync(int id, int userId, string userLogin, bool canReadOwned, CancellationToken ct)
    {
        var participated = ParticipatedActaIds(userLogin);
        var accessible = db.Set<Acta>().Where(a => a.ActaId == id &&
            (a.CreatedByUser == userId || participated.Contains(a.ActaId)));
        return GetDetailAsync(accessible, userId, canReadOwned, ct);
    }
}
