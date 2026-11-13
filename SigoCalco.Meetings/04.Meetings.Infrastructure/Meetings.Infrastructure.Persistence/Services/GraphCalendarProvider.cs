using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;

namespace Meetings.Infrastructure.Persistence.Services;

// App-only Entra credentials are independent of the JWT used to authorize the SigoCalco request.
public sealed class GraphAccessTokenProvider(HttpClient http, CalendarOptions options)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string? token;
    private DateTimeOffset expiresAt;
    public async Task<string> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (token is not null && expiresAt > DateTimeOffset.UtcNow.AddMinutes(2)) return token;
            if (!Guid.TryParse(options.TenantId, out var tenant) || !Guid.TryParse(options.ClientId, out var client) || string.IsNullOrWhiteSpace(options.ClientSecret))
                throw new IntegrationFailure("Configure TenantId, ClientId y ClientSecret de Entra fuera del repositorio.");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{tenant:D}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = client.ToString("D"), ["client_secret"] = options.ClientSecret,
                    ["grant_type"] = "client_credentials", ["scope"] = "https://graph.microsoft.com/.default"
                })
            };
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw new IntegrationFailure($"Entra rechazó la autenticación de aplicación (HTTP {(int)response.StatusCode}). Revise credenciales y consentimiento.");
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            token = document.RootElement.GetProperty("access_token").GetString() ?? throw new IntegrationFailure("Entra no devolvió un token.");
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(document.RootElement.GetProperty("expires_in").GetInt32());
            return token;
        }
        finally { gate.Release(); }
    }
}

public sealed class GraphCalendarProvider(HttpClient http, GraphAccessTokenProvider tokens, CalendarOptions options) : IMeetingCalendarProvider
{
    public async Task<ProviderResult> SynchronizeAsync(MeetingIntegrationRequest input, CancellationToken ct)
    {
        // This gate runs before even requesting an Entra token. Default configuration cannot send invitations.
        if (!options.AllowExternalInvitations) throw new IntegrationFailure("Envíos reales deshabilitados. Se requiere configuración y autorización de la prueba Microsoft 365.");
        if (!string.Equals(input.OrganizerEmail, options.OrganizerEmail, StringComparison.OrdinalIgnoreCase) ||
            options.OrganizerEmail.EndsWith(".invalid", StringComparison.OrdinalIgnoreCase))
            throw new IntegrationFailure("El organizador no coincide con el buzón corporativo autorizado.");
        _ = ActaApplication.Email(options.OrganizerEmail);
        foreach (var attendee in input.Attendees) _ = ActaApplication.Email(attendee.Email);
        var root = $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(options.OrganizerEmail)}/events";
        var existing = !string.IsNullOrEmpty(input.ExternalId);
        if (input.ExternalId?.StartsWith("SIMULATED:", StringComparison.Ordinal) == true)
            throw new IntegrationFailure("Una simulación no puede convertirse automáticamente en una invitación real.");
        using var request = new HttpRequestMessage(existing ? HttpMethod.Get : HttpMethod.Post,
            existing ? root + "/" + Uri.EscapeDataString(input.ExternalId!) + "?$select=id,onlineMeeting" : root);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GetAsync(ct));
        request.Headers.Add("Prefer", "IdType=\"ImmutableId\"");
        if (!existing) request.Content = JsonContent.Create(new
        {
            subject = input.Title,
            start = new { dateTime = input.StartAtUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), timeZone = "UTC" },
            end = new { dateTime = input.EndAtUtc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture), timeZone = "UTC" },
            location = new { displayName = input.Location ?? "Microsoft Teams" },
            isOnlineMeeting = true, onlineMeetingProvider = "teamsForBusiness",
            transactionId = input.IdempotencyKey,
            attendees = input.Attendees.Select(a => new { emailAddress = new { address = a.Email, name = a.Name }, type = "required" }).ToArray()
        });
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var retry = response.Headers.RetryAfter;
            var wait = retry is null ? "" : $" Respete Retry-After: {retry}.";
            throw new IntegrationFailure($"Microsoft Graph respondió HTTP {(int)response.StatusCode}.{wait} Revise permisos/configuración antes del reintento controlado.");
        }
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = document.RootElement;
        var eventId = result.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(eventId) || eventId.Length > 512) throw new IntegrationFailure("Graph no devolvió un identificador de evento válido. Reintente con la misma operación.");
        string? joinUrl = null;
        if (result.TryGetProperty("onlineMeeting", out var online) && online.ValueKind == JsonValueKind.Object &&
            online.TryGetProperty("joinUrl", out var join) && join.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(join.GetString(), UriKind.Absolute, out var uri) && uri.Scheme == "https") joinUrl = uri.AbsoluteUri;
        return new(eventId, false, joinUrl);
    }
}
