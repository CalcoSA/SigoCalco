using System.Text.Json;

namespace Meetings.Application.Dtos;

public sealed record ActaTypeDto(int ActaTypeId, string Name);
public sealed record CreateTemplateDto(int ActaTypeId, string Name, string Scope, string? SolutionCenterCode);
public sealed record CreatedTemplateDto(int TemplateId);
public sealed record CreateTemplateWithVersionDto(int ActaTypeId, string Name, string Scope, string? SolutionCenterCode,
    IReadOnlyList<CreateTemplateSectionDto>? Sections);
public sealed record CreatedTemplateWithVersionDto(int TemplateId, int TemplateVersionId, int VersionNumber, IReadOnlyList<TemplateSectionDto> Sections);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateTemplateDto(string Name);
public sealed record TemplateVersionDetailDto(int TemplateId, string Name, int ActaTypeId, string ActaTypeName,
    string Scope, string? SolutionCenterCode, int TemplateVersionId, int VersionNumber, int CreatedByUser,
    DateTime CreatedAt, IReadOnlyList<TemplateSectionDto> Sections);
public sealed record CreateTemplateVersionDto(IReadOnlyList<CreateTemplateSectionDto>? Sections);
public sealed record CreateTemplateSectionDto(string Title, string ContentType, int SortOrder, bool IsRequired, JsonElement? ConfigurationJson);
public sealed record TemplateSectionDto(int TemplateSectionId, string Title, string ContentType, int SortOrder, bool IsRequired, JsonElement? ConfigurationJson);
public sealed record TemplateVersionDto(int TemplateVersionId, int VersionNumber, int CreatedByUser, DateTime CreatedAt, IReadOnlyList<TemplateSectionDto> Sections);
public sealed record TemplateDto(int TemplateId, int ActaTypeId, string Name, string Scope, string? SolutionCenterCode, int CreatedByUser, DateTime CreatedAt, IReadOnlyList<TemplateVersionDto> Versions);
public sealed record TemplateQuery(int Page = 1, int PageSize = 20, int? ActaTypeId = null, string? Scope = null);
public sealed record PagedTemplatesDto(IReadOnlyList<TemplateDto> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

