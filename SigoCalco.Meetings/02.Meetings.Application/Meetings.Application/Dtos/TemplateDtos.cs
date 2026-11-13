using System.Text.Json;

namespace Meetings.Application.Dtos;

public sealed record ActaTypeDto(int ActaTypeId, string Name);
public sealed record CreateTemplateDto(int ActaTypeId, string Name, string Scope, string? SolutionCenterCode);
public sealed record CreatedTemplateDto(int TemplateId);
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

