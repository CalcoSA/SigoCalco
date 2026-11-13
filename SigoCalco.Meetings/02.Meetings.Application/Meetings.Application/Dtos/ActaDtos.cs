using System.Text.Json;

namespace Meetings.Application.Dtos;

public sealed record CreateActaDto(int TemplateVersionId, string Title, DateOnly DocumentDate,
    string SolutionCenterCode, IReadOnlyList<ActaSectionInput>? Sections,
    IReadOnlyList<ParticipantInput>? Participants, MeetingInput? Meeting);
public sealed record ActaSectionInput(int TemplateSectionId, JsonElement? ContentJson);
public sealed record ParticipantInput(string UserName, string Email, string ParticipantType = "EXTERNO", string? UserLogin = null);
public sealed record MeetingInput(string Modality, DateTimeOffset StartAt, DateTimeOffset EndAt,
    string TimeZone, string? LocationDescription = null, string? ExternalRoomId = null);
public sealed record IntegrationDto(string Provider, string State, bool Simulated, string? ExternalId = null,
    string? TeamsJoinUrl = null, string? Error = null);
public sealed record CreatedActaDto(int ActaId, string ActaCode, int? MeetingId, string? Modality, IntegrationDto? Integration);
