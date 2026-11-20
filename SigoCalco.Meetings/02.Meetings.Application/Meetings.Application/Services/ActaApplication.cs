using System.Net.Mail;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

public sealed partial class ActaApplication(IActaStore store, ITemplateStore templates, IMeetingsIdentity identity,
    SectionJsonValidator json, IMeetingIntegrationService integrations, MeetingNotificationComposer composer,
    IMeetingNotificationService notifications, ITemplateApplication templateApplication) : IActaApplication
{
    public async Task<CreatedActaDto> CreateAsync(CreateActaDto input, string? idempotencyKey, CancellationToken ct)
    {
        var actor = await RequireActorAsync(ct);
        if (!Guid.TryParseExact(idempotencyKey, "D", out var key) || key == Guid.Empty)
            throw MeetingsRequestException.Invalid("Idempotency-Key debe ser un UUID válido. Reutilícelo al reintentar la misma operación.");
        var source = await store.FindVersionAsync(input.TemplateVersionId, ct)
            ?? throw MeetingsRequestException.NotFound("No se encontró la versión de plantilla.");
        var template = source.Template;
        if (!MeetingsPermissions.ActiveTypeCodes.Contains(template.ActaType.Code))
            throw MeetingsRequestException.Invalid("El tipo de acta ya no está vigente.");
        // The selected version must belong to a visible template, even if its ID was supplied directly.
        MeetingsPermissions.RequireRead(template, await templates.GetPermissionsAsync(ct), actor);
        var center = Text(input.SolutionCenterCode, 64, "solutionCenterCode").ToUpperInvariant();
        if (!actor.CenterCodes.Contains(center, StringComparer.OrdinalIgnoreCase) ||
            (template.Scope == "CENTRO" && !string.Equals(template.SolutionCenterCode, center, StringComparison.OrdinalIgnoreCase)))
            throw MeetingsRequestException.Forbidden();
        if (input.DocumentDate == default) throw MeetingsRequestException.Invalid("documentDate es obligatorio.");
        var title = Text(input.Title, 250, "title");
        var sections = ValidateSections(source, input.Sections);
        var participants = ValidateParticipants(input.Participants);
        var now = DateTime.UtcNow;
        ScheduledMeeting? meeting = null;
        string? room = null;
        if (input.Meeting is { } m)
        {
            var modality = m.Modality?.Trim().ToUpperInvariant();
            if (modality is not ("PRESENCIAL" or "VIRTUAL")) throw MeetingsRequestException.Invalid("modality debe ser PRESENCIAL o VIRTUAL.");
            if (m.StartAt == default || m.EndAt <= m.StartAt || m.StartAt.UtcDateTime.Year < 1000)
                throw MeetingsRequestException.Invalid("La reunión requiere inicio y fin válidos, con fin posterior al inicio.");
            var zone = Text(m.TimeZone, 60, "timeZone");
            try { _ = TimeZoneInfo.FindSystemTimeZoneById(zone); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            { throw MeetingsRequestException.Invalid("timeZone no es una zona horaria reconocida."); }
            var location = m.LocationDescription is null ? null : Text(m.LocationDescription, 250, "locationDescription");
            room = m.ExternalRoomId is null ? null : Text(m.ExternalRoomId, 100, "externalRoomId");
            var reserve = m.ReserveRoom ?? room is not null;
            if (m.ReserveRoom == false && room is not null)
                throw MeetingsRequestException.Invalid("reserveRoom=false requiere externalRoomId nulo.");
            if (reserve && (modality != "PRESENCIAL" || room is null))
                throw MeetingsRequestException.Invalid("La reserva requiere modalidad PRESENCIAL y externalRoomId.");
            if (modality == "VIRTUAL" && room is not null) throw MeetingsRequestException.Invalid("Una reunión virtual no admite reserva de sala.");
            meeting = new ScheduledMeeting { Modality = modality, StartAtUtc = m.StartAt.UtcDateTime, EndAtUtc = m.EndAt.UtcDateTime,
                TimeZone = zone, LocationDescription = location, OrganizerUserId = actor.UserId, CreatedAt = now, UpdatedAt = now };
        }
        var acta = new Acta { ActaCode = $"ACT-{key:N}", ActaTypeId = template.ActaTypeId,
            TemplateVersionId = source.Version.TemplateVersionId, Title = title, DocumentDate = input.DocumentDate,
            SolutionCenterCode = center, CreatedByUser = actor.UserId, CreatedAt = now, UpdatedAt = now };
        var drafts = composer.Compose(acta, meeting, participants, actor);
        var result = await store.CreateAsync(new NewActa(acta, sections, participants, meeting, room, drafts), ct);
        // Local commit precedes external work. A cancelled request leaves durable pending work for operator recovery.
        if (result.MeetingId is { } meetingId)
        {
            if (room is not null) result = result with { Integration = await integrations.SynchronizeAsync(meetingId, ct) };
        }
        return result with { Notifications = await notifications.SendForActaAsync(result.ActaId, ct) };
    }
    private IReadOnlyList<ActaSection> ValidateSections(ActaTemplate source, IReadOnlyList<ActaSectionInput>? inputs)
    {
        if (inputs is null || inputs.Count > 100 || inputs.Any(s => s is null) ||
            inputs.Select(s => s.TemplateSectionId).Distinct().Count() != inputs.Count)
            throw MeetingsRequestException.Invalid("sections debe contener hasta 100 secciones distintas.");
        var definitions = source.Version.Sections.ToDictionary(s => s.TemplateSectionId);
        if (inputs.Any(s => !definitions.ContainsKey(s.TemplateSectionId)))
            throw MeetingsRequestException.Invalid("Una sección no pertenece a la versión seleccionada.");
        var supplied = inputs.ToDictionary(s => s.TemplateSectionId);
        var sections = new List<ActaSection>();
        foreach (var definition in definitions.Values)
        {
            supplied.TryGetValue(definition.TemplateSectionId, out var section);
            var configuration = definition.ConfigurationJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(definition.ConfigurationJson);
            json.ValidateContent(definition.ContentType, configuration, section?.ContentJson, definition.IsRequired);
            if (section is not null) sections.Add(new ActaSection
            {
                TemplateVersionId = source.Version.TemplateVersionId, TemplateSectionId = definition.TemplateSectionId,
                ContentJson = SectionJsonValidator.HasValue(section.ContentJson) ? section.ContentJson!.Value.GetRawText() : null
            });
        }
        return sections;
    }
    private static IReadOnlyList<Participant> ValidateParticipants(IReadOnlyList<ParticipantInput>? inputs)
    {
        if (inputs is null || inputs.Count > 100)
            throw MeetingsRequestException.Invalid("participants debe ser una lista de hasta 100 personas.");
        var participants = new List<Participant>();
        var emails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var participant in inputs)
        {
            if (participant is null) throw MeetingsRequestException.Invalid("Un participante no puede ser nulo.");
            var email = Email(participant.Email);
            if (!emails.Add(email)) throw MeetingsRequestException.Invalid("Los participantes contienen correos duplicados.");
            var type = participant.ParticipantType?.Trim().ToUpperInvariant();
            if (type is not ("INTERNO" or "EXTERNO")) throw MeetingsRequestException.Invalid("participantType debe ser INTERNO o EXTERNO.");
            if (type == "EXTERNO" && participant.UserLogin is not null)
                throw MeetingsRequestException.Invalid("Los participantes EXTERNO requieren userLogin nulo.");
            participants.Add(new Participant { UserName = Text(participant.UserName, 250, "userName"), Email = email,
                ParticipantType = type, UserLogin = type == "INTERNO" ? Text(participant.UserLogin, 60, "userLogin") : null });
        }
        return participants;
    }
    private static string Text(string? value, int max, string field) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max ? value.Trim()
        : throw MeetingsRequestException.Invalid($"{field} requiere entre 1 y {max} caracteres.");
    public static string Email(string? value)
    {
        var email = Text(value, 254, "email").ToLowerInvariant();
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email || !email.Contains('@'))
            throw MeetingsRequestException.Invalid("El correo electrónico no es válido.");
        return email;
    }
}
