using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public interface ITemplateApplication
{
    Task<IReadOnlyList<ActaTypeDto>> GetTypesAsync(CancellationToken ct);
    Task<CreatedTemplateDto> CreateAsync(CreateTemplateDto request, CancellationToken ct);
    Task<TemplateVersionDto> CreateVersionAsync(int templateId, CreateTemplateVersionDto request, CancellationToken ct);
    Task<PagedTemplatesDto> GetAsync(TemplateQuery query, CancellationToken ct);
    Task<CreatedTemplateWithVersionDto> CreateWithVersionAsync(CreateTemplateWithVersionDto request, CancellationToken ct);
    Task<IReadOnlyList<TemplateVersionDto>> GetVersionsAsync(int templateId, CancellationToken ct);
    Task<TemplateVersionDetailDto> GetVersionAsync(int templateVersionId, CancellationToken ct);
    Task<TemplateDto> UpdateAsync(int templateId, UpdateTemplateDto request, CancellationToken ct);
}

