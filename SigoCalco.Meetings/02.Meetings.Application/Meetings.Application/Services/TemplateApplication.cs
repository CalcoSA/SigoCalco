using System.Text;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

public sealed class TemplateApplication(ITemplateStore store, IMeetingsIdentity identity, SectionJsonValidator json) : ITemplateApplication
{
    public async Task<IReadOnlyList<ActaTypeDto>> GetTypesAsync(CancellationToken ct)
    {
        await identity.GetAsync(ct);
        return (await store.GetTypesAsync(ct)).Select(t => new ActaTypeDto(t.ActaTypeId, t.Name)).ToArray();
    }

    public async Task<CreatedTemplateDto> CreateAsync(CreateTemplateDto request, CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        var type = (await store.GetTypesAsync(ct)).SingleOrDefault(t => t.ActaTypeId == request.ActaTypeId)
            ?? throw MeetingsRequestException.Invalid("El tipo de acta no existe.");
        var name = RequiredText(request.Name, 200, "name");
        var scope = NormalizeScope(request.Scope);
        if (type.Code == "DISCIPLINARIA" && scope != "GLOBAL")
            throw MeetingsRequestException.Invalid("Las plantillas disciplinarias solo admiten alcance GLOBAL.");
        var permissions = await store.GetPermissionsAsync(ct);
        RequirePermission(permissions, actor, Key(type.Code, "PLANTILLA_CREACION"));
        string? center = null;
        if (scope == "GLOBAL")
        {
            if (request.SolutionCenterCode is not null) throw MeetingsRequestException.Invalid("GLOBAL requiere solutionCenterCode nulo.");
        }
        else
        {
            center = RequiredText(request.SolutionCenterCode, 64, "solutionCenterCode").ToUpperInvariant();
            RequireCenter(actor, center);
        }
        return new(await store.CreateAsync(new Template
        {
            ActaTypeId = type.ActaTypeId, Name = name, Scope = scope, SolutionCenterCode = center,
            CreatedByUser = actor.UserId, CreatedAt = DateTime.UtcNow
        }, ct));
    }

    public async Task<TemplateVersionDto> CreateVersionAsync(int templateId, CreateTemplateVersionDto request, CancellationToken ct)
    {
        if (templateId <= 0) throw MeetingsRequestException.Invalid("templateId debe ser positivo.");
        var actor = await identity.GetAsync(ct);
        var template = await store.FindAsync(templateId, ct) ?? throw MeetingsRequestException.NotFound("No se encontró la plantilla.");
        RequirePermission(await store.GetPermissionsAsync(ct), actor, Key(template.ActaType.Code, "VERSION_CREACION"));
        if (template.Scope == "CENTRO") RequireCenter(actor, template.SolutionCenterCode!);
        if (request.Sections is null || request.Sections.Count is < 1 or > 100)
            throw MeetingsRequestException.Invalid("Se requieren entre 1 y 100 secciones.");
        var orders = new HashSet<int>();
        var sections = new List<TemplateSection>();
        foreach (var section in request.Sections)
        {
            if (section is null) throw MeetingsRequestException.Invalid("Una sección no puede ser nula.");
            if (section.SortOrder < 1 || !orders.Add(section.SortOrder))
                throw MeetingsRequestException.Invalid("sortOrder debe ser positivo y único.");
            var contentType = RequiredText(section.ContentType, 20, "contentType").ToUpperInvariant();
            json.ValidateConfiguration(contentType, section.ConfigurationJson);
            sections.Add(new TemplateSection
            {
                Title = RequiredText(section.Title, 200, "title"), ContentType = contentType,
                SortOrder = section.SortOrder, IsRequired = section.IsRequired,
                ConfigurationJson = SectionJsonValidator.HasValue(section.ConfigurationJson) ? section.ConfigurationJson!.Value.GetRawText() : null
            });
        }
        if (!orders.SetEquals(Enumerable.Range(1, sections.Count)))
            throw MeetingsRequestException.Invalid("sortOrder debe ser consecutivo desde 1.");
        var version = await store.CreateVersionAsync(new TemplateVersion
        {
            TemplateId = templateId, ActaTypeId = template.ActaTypeId, CreatedByUser = actor.UserId,
            CreatedAt = DateTime.UtcNow, Sections = sections.OrderBy(s => s.SortOrder).ToList()
        }, ct);
        return MapVersion(version);
    }

    public async Task<PagedTemplatesDto> GetAsync(TemplateQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw MeetingsRequestException.Invalid("Paginación inválida: page >= 1 y pageSize entre 1 y 100.");
        if (query.ActaTypeId is <= 0) throw MeetingsRequestException.Invalid("actaTypeId debe ser positivo.");
        var scope = query.Scope is null ? null : NormalizeScope(query.Scope);
        var actor = await identity.GetAsync(ct);
        var permissions = await store.GetPermissionsAsync(ct);
        var readableTypes = (await store.GetTypesAsync(ct)).Where(t => Allowed(permissions, actor, Key(t.Code, "PLANTILLA_LECTURA")))
            .Select(t => t.ActaTypeId).ToArray();
        if (readableTypes.Length == 0 || (query.ActaTypeId.HasValue && !readableTypes.Contains(query.ActaTypeId.Value)))
            throw MeetingsRequestException.Forbidden();
        var result = await store.GetAsync(query.Page, query.PageSize, query.ActaTypeId, scope,
            readableTypes, actor.CenterCodes, Allowed(permissions, actor, "VIEW_ALL_SOLUTION_CENTERS"), ct);
        return new(result.Items.Select(t => new TemplateDto(t.TemplateId, t.ActaTypeId, t.Name, t.Scope, t.SolutionCenterCode,
            t.CreatedByUser, t.CreatedAt, t.Versions.OrderBy(v => v.VersionNumber).Select(MapVersion).ToArray())).ToArray(),
            query.Page, query.PageSize, result.TotalCount);
    }

    public static string NormalizeRole(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    public static string Key(string code, string operation) => $"ACTA_{code}_{operation}";
    public static bool Allowed(IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor, string key) =>
        permissions.TryGetValue(key, out var value) && value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeRole).Intersect(actor.Roles.Select(NormalizeRole), StringComparer.Ordinal).Any();
    private static void RequirePermission(IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor, string key)
    {
        if (!Allowed(permissions, actor, key)) throw MeetingsRequestException.Forbidden();
    }
    private static void RequireCenter(MeetingsIdentity actor, string center)
    {
        if (!actor.CenterCodes.Contains(center, StringComparer.OrdinalIgnoreCase)) throw MeetingsRequestException.Forbidden();
    }
    private static string NormalizeScope(string? value)
    {
        var scope = value?.Trim().ToUpperInvariant();
        return scope is "GLOBAL" or "CENTRO" ? scope : throw MeetingsRequestException.Invalid("scope debe ser GLOBAL o CENTRO.");
    }
    private static string RequiredText(string? value, int max, string field)
    {
        var trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= max ? trimmed : throw MeetingsRequestException.Invalid($"{field} requiere entre 1 y {max} caracteres.");
    }
    private static TemplateVersionDto MapVersion(TemplateVersion version) => new(version.TemplateVersionId, version.VersionNumber,
        version.CreatedByUser, version.CreatedAt, version.Sections.OrderBy(s => s.SortOrder).Select(s =>
            new TemplateSectionDto(s.TemplateSectionId, s.Title, s.ContentType, s.SortOrder, s.IsRequired,
                s.ConfigurationJson is null ? null : JsonSerializer.Deserialize<JsonElement>(s.ConfigurationJson))).ToArray());
}

