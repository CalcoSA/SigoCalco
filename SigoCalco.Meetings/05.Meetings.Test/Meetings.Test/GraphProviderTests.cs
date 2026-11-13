using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Interfaces;
using Meetings.Infrastructure.Persistence.Services;

namespace Meetings.Test;

public sealed class GraphProviderTests
{
    private static CalendarOptions Options() => new()
    {
        Mode = "Graph", OrganizerEmail = "organizer@example.invalid", AllowExternalInvitations = true,
        TenantId = "11111111-1111-1111-1111-111111111111", ClientId = "22222222-2222-2222-2222-222222222222", ClientSecret = "test-placeholder"
    };
    private static MeetingIntegrationRequest Input(string? externalId = null) => new(1, "ACT-11111111111111111111111111111111", "Reunión",
        new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 1, 16, 0, 0, DateTimeKind.Utc),
        "America/Bogota", null, null, "organizer@example.test", [new("Persona", "person@example.test")], externalId);
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Bearer, string Body)> Requests = [];
        public Func<HttpRequestMessage, HttpResponseMessage> GraphResponse = _ => new(HttpStatusCode.Created)
        { Content = JsonContent.Create(new { id = "event-1", onlineMeeting = new { joinUrl = "https://teams.microsoft.com/test-response-only" } }) };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.Parameter,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return request.RequestUri!.Host == "login.microsoftonline.com"
                ? new(HttpStatusCode.OK) { Content = JsonContent.Create(new { access_token = "graph-test-token", expires_in = 3600 }) }
                : GraphResponse(request);
        }
    }
    [Fact]
    public async Task Uses_Entra_app_token_v1_event_attendees_and_stable_transaction_id()
    {
        var options = Options(); options.OrganizerEmail = "organizer@example.test";
        var handler = new RecordingHandler(); using var http = new HttpClient(handler);
        var provider = new GraphCalendarProvider(http, new GraphAccessTokenProvider(http, options), options);
        var first = await provider.SynchronizeAsync(Input(), default);
        var retry = await provider.SynchronizeAsync(Input(), default);
        Assert.False(first.Simulated); Assert.Equal("event-1", first.ExternalId);
        Assert.Equal("https://teams.microsoft.com/test-response-only", first.TeamsJoinUrl);
        Assert.Equal(first, retry);
        Assert.Equal(3, handler.Requests.Count);
        var token = handler.Requests[0]; Assert.Contains("grant_type=client_credentials", token.Body);
        Assert.Contains("graph.microsoft.com", Uri.UnescapeDataString(token.Body));
        foreach (var sent in handler.Requests.Skip(1))
        {
            Assert.Equal("graph-test-token", sent.Bearer); Assert.Contains("/v1.0/users/", sent.Uri.AbsolutePath);
            using var document = JsonDocument.Parse(sent.Body); var body = document.RootElement;
            Assert.True(body.GetProperty("isOnlineMeeting").GetBoolean());
            Assert.Equal("teamsForBusiness", body.GetProperty("onlineMeetingProvider").GetString());
            Assert.Equal(Input().IdempotencyKey, body.GetProperty("transactionId").GetString());
            Assert.Equal("UTC", body.GetProperty("start").GetProperty("timeZone").GetString());
            Assert.Equal("person@example.test", body.GetProperty("attendees")[0].GetProperty("emailAddress").GetProperty("address").GetString());
        }
    }
    [Fact]
    public async Task Event_without_join_url_is_recovered_by_GET_without_another_invitation()
    {
        var options = Options(); options.OrganizerEmail = "organizer@example.test";
        var handler = new RecordingHandler { GraphResponse = _ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new { id = "event-1", onlineMeeting = (object?)null }) } };
        using var http = new HttpClient(handler);
        var provider = new GraphCalendarProvider(http, new GraphAccessTokenProvider(http, options), options);
        var pending = await provider.SynchronizeAsync(Input(), default);
        Assert.Equal("event-1", pending.ExternalId); Assert.Null(pending.TeamsJoinUrl);
        await provider.SynchronizeAsync(Input(pending.ExternalId), default);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method); Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
    }
    [Theory]
    [InlineData("disabled")]
    [InlineData("organizer")]
    [InlineData("simulation")]
    public async Task Safety_gates_do_not_make_any_network_call(string reason)
    {
        var options = Options(); options.OrganizerEmail = "organizer@example.test";
        if (reason == "disabled") options.AllowExternalInvitations = false;
        if (reason == "organizer") options.OrganizerEmail = "other@example.test";
        var handler = new RecordingHandler(); using var http = new HttpClient(handler);
        var provider = new GraphCalendarProvider(http, new GraphAccessTokenProvider(http, options), options);
        await Assert.ThrowsAsync<IntegrationFailure>(() => provider.SynchronizeAsync(Input(reason == "simulation" ? "SIMULATED:1" : null), default));
        Assert.Empty(handler.Requests);
    }
    [Theory]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Graph_errors_are_sanitized_and_retry_after_is_preserved(int code)
    {
        var options = Options(); options.OrganizerEmail = "organizer@example.test";
        var handler = new RecordingHandler { GraphResponse = _ =>
        {
            var response = new HttpResponseMessage((HttpStatusCode)code) { Content = new StringContent("sensitive-provider-body") };
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(60)); return response;
        } };
        using var http = new HttpClient(handler);
        var provider = new GraphCalendarProvider(http, new GraphAccessTokenProvider(http, options), options);
        var error = await Assert.ThrowsAsync<IntegrationFailure>(() => provider.SynchronizeAsync(Input(), default));
        Assert.Contains(code.ToString(), error.Message); Assert.Contains("Retry-After: 60", error.Message);
        Assert.DoesNotContain("sensitive-provider-body", error.Message);
    }
}
