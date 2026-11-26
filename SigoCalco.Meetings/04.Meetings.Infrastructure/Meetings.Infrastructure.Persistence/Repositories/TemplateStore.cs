using System.Data;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public sealed class TemplateStore(MeetingsDbContext context) : ITemplateStore
{
    public async Task<IReadOnlyList<ActaType>> GetTypesAsync(CancellationToken ct) =>
        await context.Set<ActaType>().AsNoTracking().Where(t => MeetingsPermissions.ActiveTypeCodes.Contains(t.Code)).OrderBy(t => t.ActaTypeId).ToListAsync(ct);
    public Task<Template?> FindAsync(int id, CancellationToken ct) =>
        context.Set<Template>().AsNoTracking().Include(t => t.ActaType).SingleOrDefaultAsync(t => t.TemplateId == id, ct);
    public async Task<int> CreateAsync(Template template, CancellationToken ct)
    {
        await RequireAvailableNameAsync(template.Name, null, ct);
        context.Add(template);
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysql && IsNameConflict(mysql))
        { context.ChangeTracker.Clear(); throw DuplicateName(); }
        return template.TemplateId;
    }
    private async Task RequireAvailableNameAsync(string name, int? exceptId, CancellationToken ct)
    {
        var normalized = name.Trim().ToLowerInvariant();
        if (await context.Set<Template>().AnyAsync(t => (!exceptId.HasValue || t.TemplateId != exceptId.Value) &&
            EF.Property<string>(t, "NormalizedName") == normalized, ct)) throw DuplicateName();
    }
    private static bool IsNameConflict(MySqlException error) => error.Number == 1062 &&
        error.Message.Contains("UX_meetings_template_name_normalized", StringComparison.OrdinalIgnoreCase);
    private static MeetingsRequestException DuplicateName() => new(409, "Ya existe una plantilla con ese nombre, independientemente de su ámbito o centro.");
    public async Task<IReadOnlyDictionary<string, string>> GetPermissionsAsync(CancellationToken ct) =>
        await context.Set<MeetingsPermission>().AsNoTracking().ToDictionaryAsync(p => p.PermissionKey, p => p.PermissionValue, ct);

    public async Task<Template> CreateWithVersionAsync(Template template, CancellationToken ct)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            await CreateAsync(template, ct);
            await transaction.CommitAsync(ct);
            return template;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); context.ChangeTracker.Clear(); throw; }
    }
    public async Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(int templateId, CancellationToken ct) =>
        await context.Set<TemplateVersion>().AsNoTracking().Where(v => v.TemplateId == templateId).OrderBy(v => v.VersionNumber)
            .Include(v => v.Sections.OrderBy(s => s.SortOrder)).AsSplitQuery().ToListAsync(ct);
    public Task<TemplateVersion?> FindVersionAsync(int versionId, CancellationToken ct) =>
        context.Set<TemplateVersion>().AsNoTracking().Include(v => v.Sections.OrderBy(s => s.SortOrder))
            .SingleOrDefaultAsync(v => v.TemplateVersionId == versionId, ct);
    public async Task UpdateNameAsync(int templateId, string name, CancellationToken ct)
    {
        await RequireAvailableNameAsync(name, templateId, ct);
        try
        {
            if (await context.Set<Template>().Where(t => t.TemplateId == templateId).ExecuteUpdateAsync(s => s.SetProperty(t => t.Name, name), ct) != 1)
                throw MeetingsRequestException.NotFound("No se encontró la plantilla.");
        }
        catch (MySqlException ex) when (IsNameConflict(ex)) { throw DuplicateName(); }
    }

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
        IReadOnlyCollection<int> readableTypeIds, IReadOnlyCollection<string> centerCodes, TemplateVisibility visibility, CancellationToken ct, bool latestVersionOnly = false)
    {
        var query = context.Set<Template>().AsNoTracking().Where(t => readableTypeIds.Contains(t.ActaTypeId));
        if (actaTypeId.HasValue) query = query.Where(t => t.ActaTypeId == actaTypeId);
        if (scope is not null) query = query.Where(t => t.Scope == scope);
        query = query.Where(t => t.Scope == "GLOBAL" && visibility.Global || t.Scope == "CENTRO" &&
            (visibility.AllCenters || visibility.OwnCenters && centerCodes.Contains(t.SolutionCenterCode!)));
        // A stable snapshot keeps pagination metadata and nested collections consistent.
        var isolation = context.Database.IsMySql() ? IsolationLevel.RepeatableRead : IsolationLevel.Serializable;
        await using var transaction = await context.Database.BeginTransactionAsync(isolation, ct);
        var total = await query.CountAsync(ct);
        var paged = query.OrderBy(t => t.TemplateId).Skip((page - 1) * pageSize).Take(pageSize);
        var included = latestVersionOnly
            ? paged.Include(t => t.Versions.OrderByDescending(v => v.VersionNumber).Take(1)).ThenInclude(v => v.Sections.OrderBy(s => s.SortOrder))
            : paged.Include(t => t.Versions.OrderBy(v => v.VersionNumber)).ThenInclude(v => v.Sections.OrderBy(s => s.SortOrder));
        var items = await included.AsSplitQuery().ToListAsync(ct);
        await transaction.CommitAsync(ct);
        return new(items, total);
    }
}

