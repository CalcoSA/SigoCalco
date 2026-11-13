using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Interfaces;

public sealed record TemplatePage(IReadOnlyList<Template> Items, int TotalCount);
public interface ITemplateStore
{
    Task<IReadOnlyList<ActaType>> GetTypesAsync(CancellationToken ct);
    Task<Template?> FindAsync(int id, CancellationToken ct);
    Task<int> CreateAsync(Template template, CancellationToken ct);
    Task<TemplateVersion> CreateVersionAsync(TemplateVersion version, CancellationToken ct);
    Task<TemplatePage> GetAsync(int page, int pageSize, int? actaTypeId, string? scope,
        IReadOnlyCollection<int> readableTypeIds, IReadOnlyCollection<string> centerCodes, bool allCenters, CancellationToken ct);
    Task<IReadOnlyDictionary<string, string>> GetPermissionsAsync(CancellationToken ct);
}

