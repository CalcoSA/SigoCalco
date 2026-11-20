using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Meetings.Application.Interfaces;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

public sealed class MeetingNotificationComposer
{
    public IReadOnlyList<MeetingNotification> Compose(Acta acta, ScheduledMeeting? meeting,
        IReadOnlyList<Participant> participants, MeetingsIdentity actor)
    {
        var recipients = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? email, string? name)
        {
            var normalized = email?.Trim().ToLowerInvariant();
            if (normalized is not null && normalized.Contains('@') && normalized.Length <= 254 && MailAddress.TryCreate(normalized, out var address) && address.Address == normalized)
                recipients.TryAdd(normalized, name ?? "");
        }
        foreach (var participant in participants) Add(participant.Email, participant.UserName);
        var lines = new List<string>
        {
            "Se ha creado un acta.", $"Título: {acta.Title}", $"Acta: {acta.ActaCode}",
            $"Fecha del documento: {acta.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
        };
        if (meeting is not null)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(meeting.TimeZone);
            var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(meeting.StartAtUtc, DateTimeKind.Utc), zone);
            var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(meeting.EndAtUtc, DateTimeKind.Utc), zone);
            lines.AddRange([$"Inicio: {start.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}",
                $"Fin: {end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}", $"Zona horaria: {meeting.TimeZone}", $"Modalidad: {meeting.Modality}"]);
            if (meeting.Modality == "PRESENCIAL" && meeting.LocationDescription is not null) lines.Add($"Lugar: {meeting.LocationDescription}");
        }
        if (!string.IsNullOrWhiteSpace(actor.UserName) || !string.IsNullOrWhiteSpace(actor.Email))
            lines.Add($"Creador/organizador: {actor.UserName} {actor.Email}".Trim());
        lines.Add("Esta notificación no confirma una reserva de sala ni incluye un enlace de videoconferencia.");
        var text = string.Join("\n", lines);
        var html = "<!doctype html><html lang=\"es\"><body>" + string.Concat(lines.Select(line => "<p>" + WebUtility.HtmlEncode(line) + "</p>")) + "</body></html>";
        return recipients.Select(pair => new MeetingNotification
        {
            RecipientEmail = pair.Key, RecipientName = pair.Value,
            MessageId = acta.ActaCode + "." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pair.Key))).ToLowerInvariant() + "@sigocalco.local",
            Subject = ("SigoCalco - " + acta.Title).Replace('\r', ' ').Replace('\n', ' '), TextBody = text, HtmlBody = html,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        }).ToArray();
    }
}
