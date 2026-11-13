using System.Data;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed class TemplateStore(MeetingsDbContext context) : ITemplateStore
{
    public async Task<IReadOnlyList<ActaType>> GetTypesAsync(CancellationToken ct) =>
        await context.Set<ActaType>().AsNoTracking().OrderBy(t => t.ActaTypeId).ToListAsync(ct);
    public Task<Template?> FindAsync(int id, CancellationToken ct) =>
        context.Set<Template>().AsNoTracking().Include(t => t.ActaType).SingleOrDefaultAsync(t => t.TemplateId == id, ct);
    public async Task<int> CreateAsync(Template template, CancellationToken ct)
    {
        context.Add(template);
        await context.SaveChangesAsync(ct);
        return template.TemplateId;
    }
    public async Task<IReadOnlyDictionary<string, string>> GetPermissionsAsync(CancellationToken ct) =>
        await context.Set<MeetingsPermission>().AsNoTracking().ToDictionaryAsync(p => p.PermissionKey, p => p.PermissionValue, ct);

    public async Task<TemplateVersion> CreateVersionAsync(TemplateVersion version, CancellationToken ct)
    {
        // ReadCommitted + parent row lock serializes numbering even when the template has no versions.
        var isolation = context.Database.IsMySql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
        await using var transaction = await context.Database.BeginTransactionAsync(isolation, ct);
        try
        {
            if (context.Database.IsMySql())
            {
                var locked = await context.Set<Template>().FromSqlInterpolated(
                    $"SELECT * FROM sigocalco_meetings_template WHERE template_id = {version.TemplateId} FOR UPDATE")
                    .AsNoTracking().ToListAsync(ct);
                if (locked.Count == 0) throw MeetingsRequestException.NotFound("No se encontró la plantilla.");
            }
            var last = await context.Set<TemplateVersion>().Where(v => v.TemplateId == version.TemplateId)
                .MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
            if (last == int.MaxValue) throw new MeetingsRequestException(409, "Se agotó la numeración de versiones.");
            version.VersionNumber = last + 1;
            context.Add(version);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return version;
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number: 1062 })
        {
            await transaction.RollbackAsync(ct);
            context.ChangeTracker.Clear();
            throw new MeetingsRequestException(409, "Conflicto al crear la versión; vuelva a intentar.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<TemplatePage> GetAsync(int page, int pageSize, int? actaTypeId, string? scope,
        IReadOnlyCollection<int> readableTypeIds, IReadOnlyCollection<string> centerCodes, bool allCenters, CancellationToken ct)
    {
        var query = context.Set<Template>().AsNoTracking().Where(t => readableTypeIds.Contains(t.ActaTypeId));
        if (actaTypeId.HasValue) query = query.Where(t => t.ActaTypeId == actaTypeId);
        if (scope is not null) query = query.Where(t => t.Scope == scope);
        if (!allCenters) query = query.Where(t => t.Scope == "GLOBAL" || centerCodes.Contains(t.SolutionCenterCode!));
        // A stable snapshot keeps pagination metadata and nested collections consistent.
        var isolation = context.Database.IsMySql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable;
        await using var transaction = await context.Database.BeginTransactionAsync(isolation, ct);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(t => t.TemplateId).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(t => t.Versions.OrderBy(v => v.VersionNumber))
            .ThenInclude(v => v.Sections.OrderBy(s => s.SortOrder))
            .AsSplitQuery().ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(items, total);
    }
}

