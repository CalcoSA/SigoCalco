using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Interfaces;

public sealed record ActaTemplate(Template Template, TemplateVersion Version);
public sealed record NewActa(Acta Acta, IReadOnlyList<ActaSection> Sections, IReadOnlyList<Participant> Participants,
    ScheduledMeeting? Meeting, string? RoomId, string? OrganizerEmail);
public interface IActaStore
{
    Task<ActaTemplate?> FindVersionAsync(int versionId, CancellationToken ct);
    Task<CreatedActaDto> CreateAsync(NewActa input, CancellationToken ct);
}
public interface IActaApplication
{
    Task<CreatedActaDto> CreateAsync(CreateActaDto input, string? idempotencyKey, CancellationToken ct);
}
