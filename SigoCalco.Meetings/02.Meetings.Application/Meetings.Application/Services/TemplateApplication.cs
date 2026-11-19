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
        => new(await store.CreateAsync(await BuildTemplateAsync(request, ct), ct));

    private async Task<Template> BuildTemplateAsync(CreateTemplateDto request, CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        var type = (await store.GetTypesAsync(ct)).SingleOrDefault(t => t.ActaTypeId == request.ActaTypeId)
            ?? throw MeetingsRequestException.Invalid("El tipo de acta no existe.");
        var name = RequiredText(request.Name, 200, "name");
        var scope = NormalizeScope(request.Scope);
        var permissions = await store.GetPermissionsAsync(ct);
        string? center = null;
        if (scope == "GLOBAL")
        {
            if (request.SolutionCenterCode is not null) throw MeetingsRequestException.Invalid("GLOBAL requiere solutionCenterCode nulo.");
        }
        else
        {
            center = RequiredText(request.SolutionCenterCode, 64, "solutionCenterCode").ToUpperInvariant();
        }
        var template = new Template
        {
            ActaTypeId = type.ActaTypeId, Name = name, Scope = scope, SolutionCenterCode = center,
            CreatedByUser = actor.UserId, CreatedAt = DateTime.UtcNow
        };
        MeetingsPermissions.RequireManage(template, permissions, actor);
        return template;
    }

    public async Task<CreatedTemplateWithVersionDto> CreateWithVersionAsync(CreateTemplateWithVersionDto request, CancellationToken ct)
    {
        var template = await BuildTemplateAsync(new(request.ActaTypeId, request.Name, request.Scope, request.SolutionCenterCode), ct);
        template.Versions.Add(new TemplateVersion { ActaTypeId = template.ActaTypeId, VersionNumber = 1,
            CreatedByUser = template.CreatedByUser, CreatedAt = template.CreatedAt, Sections = BuildSections(request.Sections) });
        await store.CreateWithVersionAsync(template, ct);
        var version = MapVersion(template.Versions.Single());
        return new(template.TemplateId, version.TemplateVersionId, version.VersionNumber, version.Sections);
    }

    public async Task<TemplateVersionDto> CreateVersionAsync(int templateId, CreateTemplateVersionDto request, CancellationToken ct)
    {
        if (templateId <= 0) throw MeetingsRequestException.Invalid("templateId debe ser positivo.");
        var actor = await identity.GetAsync(ct);
        var template = await FindActiveTemplateAsync(templateId, ct);
        MeetingsPermissions.RequireManage(template, await store.GetPermissionsAsync(ct), actor);
        var version = await store.CreateVersionAsync(new TemplateVersion
        {
            TemplateId = templateId, ActaTypeId = template.ActaTypeId, CreatedByUser = actor.UserId,
            CreatedAt = DateTime.UtcNow, Sections = BuildSections(request.Sections)
        }, ct);
        return MapVersion(version);
    }

    private List<TemplateSection> BuildSections(IReadOnlyList<CreateTemplateSectionDto>? requested)
    {
        if (requested is null || requested.Count is < 1 or > 100)
            throw MeetingsRequestException.Invalid("Se requieren entre 1 y 100 secciones.");
        var orders = new HashSet<int>();
        var sections = new List<TemplateSection>();
        foreach (var section in requested)
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
        return sections.OrderBy(s => s.SortOrder).ToList();
    }

    public async Task<PagedTemplatesDto> GetAsync(TemplateQuery query, CancellationToken ct)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            throw MeetingsRequestException.Invalid("Paginación inválida: page >= 1 y pageSize entre 1 y 100.");
        if (query.ActaTypeId is <= 0) throw MeetingsRequestException.Invalid("actaTypeId debe ser positivo.");
        var scope = query.Scope is null ? null : NormalizeScope(query.Scope);
        var actor = await identity.GetAsync(ct);
        var permissions = await store.GetPermissionsAsync(ct);
        var readableTypes = (await store.GetTypesAsync(ct)).Select(t => t.ActaTypeId).ToArray();
        if (query.ActaTypeId.HasValue && !readableTypes.Contains(query.ActaTypeId.Value))
            throw MeetingsRequestException.Invalid("actaTypeId debe corresponder a un tipo vigente.");
        var visibility = MeetingsPermissions.Visibility(permissions, actor);
        if (!visibility.Any) throw MeetingsRequestException.Forbidden();
        var result = await store.GetAsync(query.Page, query.PageSize, query.ActaTypeId, scope,
            readableTypes, actor.CenterCodes, visibility, ct);
        return new(result.Items.Select(t => new TemplateDto(t.TemplateId, t.ActaTypeId, t.Name, t.Scope, t.SolutionCenterCode,
            t.CreatedByUser, t.CreatedAt, t.Versions.OrderBy(v => v.VersionNumber).Select(MapVersion).ToArray())).ToArray(),
            query.Page, query.PageSize, result.TotalCount);
    }

    private async Task<Template> FindActiveTemplateAsync(int id, CancellationToken ct)
    {
        if (id <= 0) throw MeetingsRequestException.Invalid("El identificador debe ser positivo.");
        var template = await store.FindAsync(id, ct);
        return template is not null && MeetingsPermissions.ActiveTypeCodes.Contains(template.ActaType.Code)
            ? template : throw MeetingsRequestException.NotFound("No se encontró una plantilla vigente.");
    }
    public async Task<IReadOnlyList<TemplateVersionDto>> GetVersionsAsync(int templateId, CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        var template = await FindActiveTemplateAsync(templateId, ct);
        MeetingsPermissions.RequireRead(template, await store.GetPermissionsAsync(ct), actor);
        return (await store.GetVersionsAsync(templateId, ct)).Select(MapVersion).ToArray();
    }
    public async Task<TemplateVersionDetailDto> GetVersionAsync(int templateVersionId, CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        var version = await store.FindVersionAsync(templateVersionId, ct) ?? throw MeetingsRequestException.NotFound("No se encontró la versión.");
        var template = await FindActiveTemplateAsync(version.TemplateId, ct);
        MeetingsPermissions.RequireRead(template, await store.GetPermissionsAsync(ct), actor);
        var dto = MapVersion(version);
        return new(template.TemplateId, template.Name, template.ActaTypeId, template.ActaType.Name, template.Scope,
            template.SolutionCenterCode, dto.TemplateVersionId, dto.VersionNumber, dto.CreatedByUser, dto.CreatedAt, dto.Sections);
    }
    public async Task<TemplateDto> UpdateAsync(int templateId, UpdateTemplateDto request, CancellationToken ct)
    {
        var actor = await identity.GetAsync(ct);
        var template = await FindActiveTemplateAsync(templateId, ct);
        MeetingsPermissions.RequireManage(template, await store.GetPermissionsAsync(ct), actor);
        template.Name = RequiredText(request.Name, 200, "name");
        await store.UpdateNameAsync(templateId, template.Name, ct);
        return new(template.TemplateId, template.ActaTypeId, template.Name, template.Scope, template.SolutionCenterCode,
            template.CreatedByUser, template.CreatedAt, (await store.GetVersionsAsync(templateId, ct)).Select(MapVersion).ToArray());
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

