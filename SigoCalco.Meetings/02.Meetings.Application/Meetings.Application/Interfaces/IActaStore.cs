using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Interfaces;

public sealed record ActaTemplate(Template Template, TemplateVersion Version);
public sealed record NewActa(Acta Acta, IReadOnlyList<ActaSection> Sections, IReadOnlyList<Participant> Participants,
    ScheduledMeeting? Meeting, string? RoomId, IReadOnlyList<MeetingNotification> Notifications);
public interface IActaStore
{
    Task<ActaTemplate?> FindVersionAsync(int versionId, CancellationToken ct);
    Task<CreatedActaDto> CreateAsync(NewActa input, CancellationToken ct);
    Task<PagedActasDto> GetOwnedAsync(int userId, ActaQuery query, CancellationToken ct);
    Task<PagedActasDto> GetSharedAsync(int userId, string userLogin, ActaQuery query, CancellationToken ct);
    Task<ActaDetailDto> GetReadableDetailAsync(int id, int userId, string userLogin, bool canReadOwned, CancellationToken ct);
    Task<ActaDetailDto> GetOwnedDetailAsync(int id, int userId, CancellationToken ct);
    Task UpdateOwnedAsync(int id, int userId, string title, DateOnly date, IReadOnlyList<ActaSection> sections, CancellationToken ct);
    Task ReplaceParticipantsAsync(int id, int userId, IReadOnlyList<Participant> participants, CancellationToken ct);
    Task UpdateAttendanceAsync(int id, int userId, int participantId, bool? attended, CancellationToken ct);
    Task ChangeStatusAsync(int id, int userId, string status, CancellationToken ct);
}
public interface IActaApplication
{
    Task<CreatedActaDto> CreateAsync(CreateActaDto input, string? idempotencyKey, CancellationToken ct);
    Task<PagedActasDto> GetAsync(ActaQuery query, CancellationToken ct);
    Task<PagedActasDto> GetSharedAsync(ActaQuery query, CancellationToken ct);
    Task<ActaDetailDto> GetByIdAsync(int id, CancellationToken ct);
    Task<ActaDetailDto> UpdateAsync(int id, UpdateActaDto input, CancellationToken ct);
    Task<ActaDetailDto> UpdateParticipantsAsync(int id, UpdateActaParticipantsDto input, CancellationToken ct);
    Task<ActaDetailDto> UpdateAttendanceAsync(int id, int participantId, UpdateAttendanceDto input, CancellationToken ct);
    Task<ActaDetailDto> ChangeStatusAsync(int id, UpdateActaStatusDto input, CancellationToken ct);
    Task<PagedAvailableTemplatesDto> GetAvailableTemplatesAsync(TemplateQuery query, CancellationToken ct);
}
