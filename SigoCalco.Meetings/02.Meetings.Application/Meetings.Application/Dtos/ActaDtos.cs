using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meetings.Application.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateActaDto(int TemplateVersionId, string Title, DateOnly DocumentDate,
    string SolutionCenterCode, IReadOnlyList<ActaSectionInput>? Sections,
    IReadOnlyList<ParticipantInput>? Participants, MeetingInput? Meeting);
public sealed record ActaSectionInput(int TemplateSectionId, JsonElement? ContentJson);
public sealed record ParticipantInput(string UserName, string Email, string ParticipantType = "EXTERNO", string? UserLogin = null);
public sealed record MeetingInput(string Modality, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string TimeZone, string? LocationDescription = null, string? ExternalRoomId = null, bool? ReserveRoom = null);
public sealed record IntegrationDto(string Provider, string State, bool Simulated, string? ExternalId = null,
    string? Error = null);
public sealed record CreatedActaDto(int ActaId, string ActaCode, int? MeetingId, string? Modality, IntegrationDto? Integration,
    Meetings.Application.Interfaces.NotificationSummaryDto? Notifications = null, string Status = "BORRADOR");

public sealed record ActaQuery(int Page = 1, int PageSize = 20);
public sealed record ActaListItemDto(int ActaId, string ActaCode, string Title, DateOnly DocumentDate,
    int ActaTypeId, string ActaTypeName, int ActaStatusId, string Status, string SolutionCenterCode,
    int CreatedByUser, DateTime CreatedAt, DateTime UpdatedAt, bool HasMeeting, string? Modality);
public sealed record PagedActasDto(IReadOnlyList<ActaListItemDto> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
public sealed record ActaSectionDto(int TemplateSectionId, string Title, string ContentType, int SortOrder,
    bool IsRequired, JsonElement? ConfigurationJson, JsonElement? ContentJson);
public sealed record ActaParticipantDto(int ParticipantId, string UserName, string Email, string ParticipantType, string? UserLogin, bool? Attended);
public sealed record ActaMeetingDto(int MeetingId, string Modality, DateTime StartAtUtc, DateTime EndAtUtc,
    string TimeZone, string? LocationDescription, int OrganizerUserId, MeetingReservationDto? Reservation);
public sealed record MeetingReservationDto(int MeetingReservationId, string? ExternalRoomId, string? ExternalReservationId,
    string ReservationStatus, bool Simulated, DateTime RequestedAt, DateTime? SynchronizedAt);
public sealed record ActaDetailDto(ActaListItemDto Acta, int TemplateId, string TemplateName, int TemplateVersionId,
    int VersionNumber, IReadOnlyList<ActaSectionDto> Sections, IReadOnlyList<ActaParticipantDto> Participants, ActaMeetingDto? Meeting);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateActaDto(string Title, DateOnly DocumentDate, IReadOnlyList<ActaSectionInput>? Sections);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateActaParticipantsDto(IReadOnlyList<ParticipantInput>? Participants);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAttendanceDto([property: JsonRequired] bool? Attended);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateActaStatusDto(string Status);
public sealed record AvailableTemplateDto(int TemplateId, string Name, int ActaTypeId, string Scope,
    string? SolutionCenterCode, int? TemplateVersionId, int? VersionNumber, IReadOnlyList<TemplateSectionDto> Sections, bool CanCreateActa);
public sealed record PagedAvailableTemplatesDto(IReadOnlyList<AvailableTemplateDto> Items, int Page, int PageSize, int TotalCount, int TotalPages);
