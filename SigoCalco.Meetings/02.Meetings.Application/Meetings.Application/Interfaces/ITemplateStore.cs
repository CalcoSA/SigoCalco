using Meetings.Domain.Models.Actas;
using Meetings.Application.Services;

namespace Meetings.Application.Interfaces;

public sealed record TemplatePage(IReadOnlyList<Template> Items, int TotalCount);
public interface ITemplateStore
{
    Task<IReadOnlyList<ActaType>> GetTypesAsync(CancellationToken ct);
    Task<Template?> FindAsync(int id, CancellationToken ct);
    Task<int> CreateAsync(Template template, CancellationToken ct);
    Task<TemplateVersion> CreateVersionAsync(TemplateVersion version, CancellationToken ct);
    Task<TemplatePage> GetAsync(int page, int pageSize, int? actaTypeId, string? scope,
        IReadOnlyCollection<int> readableTypeIds, IReadOnlyCollection<string> centerCodes, TemplateVisibility visibility, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetPermissionsAsync(CancellationToken ct);
    Task<Template> CreateWithVersionAsync(Template template, CancellationToken ct);
    Task<IReadOnlyList<TemplateVersion>> GetVersionsAsync(int templateId, CancellationToken ct);
    Task<TemplateVersion?> FindVersionAsync(int versionId, CancellationToken ct);
    Task UpdateNameAsync(int templateId, string name, CancellationToken ct);
}

