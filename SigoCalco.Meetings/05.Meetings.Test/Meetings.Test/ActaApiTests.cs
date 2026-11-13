using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class ActaApiTests
{
    public static async Task<CreateActaDto> PrepareAsync(MeetingsApiFactory factory, HttpClient client, bool grant = true, int type = 1, string scopeName = "GLOBAL")
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            db.Add(new ActaStatus { Name = "BORRADOR" });
            db.Add(new MeetingStatus { Name = "PROGRAMADA" });
            if (grant) db.Add(new MeetingsPermission { PermissionKey = $"ACTA_{new[] { "REUNION", "DISCIPLINARIA", "ADMINISTRATIVA" }[type - 1]}_CREACION", PermissionValue = "Administrador" });
            await db.SaveChangesAsync();
        }
        using var templateResponse = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(type, "Plantilla", scopeName, scopeName == "CENTRO" ? "R01" : null));
        var template = (await templateResponse.Content.ReadResultAsync<CreatedTemplateDto>())!;
        using var versionResponse = await client.PostAsJsonAsync($"/api/v1/templates/{template.TemplateId}/versions",
            new CreateTemplateVersionDto([new("Información", "FORMULARIO", 1, true, TemplateApiTests.Form)]));
        var version = (await versionResponse.Content.ReadResultAsync<TemplateVersionDto>())!;
        return new(version.TemplateVersionId, "Seguimiento", new DateOnly(2026, 11, 11), "R01",
            [new(version.Sections.Single().TemplateSectionId, JsonSerializer.Deserialize<JsonElement>("""{"fecha":"2026-11-11","hora":"10:00","lugar":"Bogotá"}"""))],
            [new("Persona de prueba", "person@example.invalid")], null);
    }
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, CreateActaDto input, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/actas") { Content = JsonContent.Create(input) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }
    public static MeetingInput Meeting(string modality) => new(modality,
        DateTimeOffset.Parse("2026-12-01T10:00:00-05:00"), DateTimeOffset.Parse("2026-12-01T11:00:00-05:00"),
        "America/Bogota", modality == "PRESENCIAL" ? "Sala principal" : null, modality == "PRESENCIAL" ? "ROOM-1" : null);

    [Fact]
    public async Task Without_meeting_is_atomic_reuses_participants_and_rejects_duplicate_operation()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var input = await PrepareAsync(factory, client);
        var key = Guid.NewGuid().ToString();
        using var first = await SendAsync(client, input, key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var result = (await first.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Null(result.MeetingId); Assert.Null(result.Integration);
        using var duplicate = await SendAsync(client, input, key);
        await AssertFailure(duplicate, HttpStatusCode.Conflict);
        using var second = await SendAsync(client, input with { Participants = [new("Changed name", "PERSON@EXAMPLE.INVALID"), new("New person", "new@example.invalid")] });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(2, await db.Set<Acta>().CountAsync()); Assert.Equal(2, await db.Set<ActaSection>().CountAsync());
        Assert.Equal(2, await db.Set<Participant>().CountAsync()); Assert.Equal(3, await db.Set<ActaParticipant>().CountAsync());
        Assert.Equal("Persona de prueba", (await db.Set<Participant>().SingleAsync(p => p.Email == "person@example.invalid")).UserName);
        Assert.All(await db.Set<Acta>().ToArrayAsync(), a => Assert.Equal(1, a.CreatedByUser));
        Assert.Empty(await db.Set<ScheduledMeeting>().ToArrayAsync());
        Assert.Empty(await db.Set<MeetingReservation>().ToArrayAsync()); Assert.Empty(await db.Set<MeetingCalendar>().ToArrayAsync());
    }

    [Theory]
    [InlineData("PRESENCIAL")]
    [InlineData("VIRTUAL")]
    public async Task Simulation_never_confirms_reservation_or_invitations(string modality)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var input = (await PrepareAsync(factory, client)) with { Meeting = Meeting(modality) };
        using var response = await SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.True(result.MeetingId > 0); Assert.True(result.Integration!.Simulated);
        Assert.Equal("SIMULADO", result.Integration.State); Assert.Null(result.Integration.TeamsJoinUrl);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var meeting = await db.Set<ScheduledMeeting>().SingleAsync();
        Assert.Equal(result.ActaId, meeting.ActaId); Assert.Equal(1, meeting.OrganizerUserId);
        Assert.Equal(15, meeting.StartAtUtc.Hour);
        if (modality == "PRESENCIAL")
        {
            var reservation = await db.Set<MeetingReservation>().SingleAsync();
            Assert.Equal("SOLICITADA", reservation.ReservationStatus); Assert.Null(reservation.SynchronizedAt);
            Assert.Empty(await db.Set<MeetingCalendar>().ToArrayAsync());
        }
        else
        {
            var calendar = await db.Set<MeetingCalendar>().SingleAsync();
            Assert.Equal("PENDIENTE", calendar.SyncStatus); Assert.Null(calendar.TeamsJoinUrl); Assert.Null(calendar.SynchronizedAt);
            Assert.Empty(await db.Set<MeetingReservation>().ToArrayAsync());
        }
        var retry = await scope.ServiceProvider.GetRequiredService<IMeetingIntegrationService>().SynchronizeAsync(meeting.MeetingId, default);
        Assert.Equal(result.Integration, retry);
    }

    [Theory]
    [InlineData("permission", 403)]
    [InlineData("center", 403)]
    [InlineData("content", 400)]
    [InlineData("section", 400)]
    [InlineData("missingSection", 400)]
    [InlineData("duplicateEmail", 400)]
    [InlineData("email", 400)]
    [InlineData("version", 404)]
    [InlineData("dates", 400)]
    [InlineData("zone", 400)]
    [InlineData("modality", 400)]
    [InlineData("key", 400)]
    public async Task Validation_writes_nothing(string fault, int status)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var input = await PrepareAsync(factory, client, fault != "permission");
        input = fault switch
        {
            "center" => input with { SolutionCenterCode = "R02" },
            "content" => input with { Sections = [input.Sections![0] with { ContentJson = JsonSerializer.Deserialize<JsonElement>("""{"fecha":42,"hora":"10:00","lugar":"X"}""") }] },
            "section" => input with { Sections = [input.Sections![0] with { TemplateSectionId = 999 }] },
            "missingSection" => input with { Sections = [] },
            "duplicateEmail" => input with { Participants = [input.Participants![0], input.Participants[0]] },
            "email" => input with { Participants = [new("X", "invalid")] },
            "version" => input with { TemplateVersionId = 999 },
            "dates" => input with { Meeting = Meeting("VIRTUAL") with { EndAt = DateTimeOffset.MinValue } },
            "zone" => input with { Meeting = Meeting("VIRTUAL") with { TimeZone = "not-a-zone" } },
            "modality" => input with { Meeting = Meeting("HIBRIDA") },
            _ => input
        };
        using var response = await SendAsync(client, input, fault == "key" ? "invalid" : null);
        await AssertFailure(response, (HttpStatusCode)status);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Empty(await db.Set<Acta>().ToArrayAsync()); Assert.Empty(await db.Set<Participant>().ToArrayAsync());
        Assert.Empty(await db.Set<ScheduledMeeting>().ToArrayAsync());
    }

    [Theory]
    [InlineData(1, "CENTRO")]
    [InlineData(2, "GLOBAL")]
    [InlineData(3, "CENTRO")]
    public async Task Respects_type_and_center_creation_permissions(int type, string scopeName)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var input = await PrepareAsync(factory, client, true, type, scopeName);
        using var result = await SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
    }

    [Theory]
    [InlineData("PRESENCIAL")]
    [InlineData("VIRTUAL")]
    public async Task Integration_failure_preserves_local_commit_and_allows_controlled_recovery(string modality)
    {
        var provider = new RecoverableProvider();
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<IRoomReservationProvider>(); services.RemoveAll<IMeetingCalendarProvider>();
            services.AddSingleton<IRoomReservationProvider>(provider); services.AddSingleton<IMeetingCalendarProvider>(provider);
        } };
        using var client = factory.Client();
        provider.Factory = factory;
        var input = (await PrepareAsync(factory, client)) with { Meeting = Meeting(modality) };
        using var response = await SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal("ERROR", result.Integration!.State); Assert.Null(result.Integration.TeamsJoinUrl);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Single(await db.Set<Acta>().ToArrayAsync()); Assert.Single(await db.Set<MeetingsLog>().ToArrayAsync());
        provider.Fail = false;
        var service = scope.ServiceProvider.GetRequiredService<IMeetingIntegrationService>();
        using var otherScope = factory.Services.CreateScope();
        var otherService = otherScope.ServiceProvider.GetRequiredService<IMeetingIntegrationService>();
        var retries = await Task.WhenAll(service.SynchronizeAsync(result.MeetingId!.Value, default),
            otherService.SynchronizeAsync(result.MeetingId.Value, default));
        Assert.All(retries, retry => Assert.Equal("SIMULADO", retry.State));
        Assert.Equal("SIMULADO", (await service.SynchronizeAsync(result.MeetingId.Value, default)).State);
        Assert.Equal(2, provider.Calls);
    }
    private sealed class RecoverableProvider : IRoomReservationProvider, IMeetingCalendarProvider
    {
        public bool Fail = true; public int Calls; public MeetingsApiFactory Factory = null!;
        public Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct) => SynchronizeAsync(request, ct);
        public async Task<ProviderResult> SynchronizeAsync(MeetingIntegrationRequest request, CancellationToken ct)
        {
            Calls++;
            await Task.Yield();
            using var scope = Factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            Assert.Null(db.Database.CurrentTransaction);
            Assert.True(await db.Set<ScheduledMeeting>().AnyAsync(m => m.MeetingId == request.MeetingId, ct));
            if (Fail) throw new IntegrationFailure("Fallo simulado recuperable.");
            return new($"SIMULATED:{request.IdempotencyKey}", true);
        }
    }

    [Fact]
    public async Task Calendar_event_id_and_pending_join_url_are_persisted_and_recovered_without_recreation()
    {
        var provider = new PendingCalendarProvider();
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<IMeetingCalendarProvider>(); services.AddSingleton<IMeetingCalendarProvider>(provider);
        } };
        using var client = factory.Client();
        var input = (await PrepareAsync(factory, client)) with { Meeting = Meeting("VIRTUAL") };
        using var response = await SendAsync(client, input);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal("PENDIENTE", result.Integration!.State); Assert.Equal("test-event", result.Integration.ExternalId);
        Assert.Null(result.Integration.TeamsJoinUrl);
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMeetingIntegrationService>();
        var recovered = await service.SynchronizeAsync(result.MeetingId!.Value, default);
        Assert.Equal("SINCRONIZADO", recovered.State); Assert.Equal("https://teams.microsoft.com/test-response-only", recovered.TeamsJoinUrl);
        await service.SynchronizeAsync(result.MeetingId.Value, default);
        Assert.Equal(2, provider.Calls);
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var calendar = await db.Set<MeetingCalendar>().SingleAsync();
        Assert.Equal("test-event", calendar.ExternalEventId); Assert.NotNull(calendar.SynchronizedAt); Assert.Null(calendar.LastError);
    }
    private sealed class PendingCalendarProvider : IMeetingCalendarProvider
    {
        public int Calls;
        public Task<ProviderResult> SynchronizeAsync(MeetingIntegrationRequest request, CancellationToken ct)
        {
            Calls++;
            if (Calls == 1) { Assert.Null(request.ExternalId); return Task.FromResult(new ProviderResult("test-event", false)); }
            Assert.Equal("test-event", request.ExternalId);
            return Task.FromResult(new ProviderResult("test-event", false, "https://teams.microsoft.com/test-response-only"));
        }
    }

    [Fact]
    public async Task Database_error_rolls_back_acta_sections_and_participants()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var input = await PrepareAsync(factory, client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var acta = new Acta { ActaCode = "ROLLBACK", ActaTypeId = 1, TemplateVersionId = input.TemplateVersionId,
            SolutionCenterCode = "R01", Title = "Rollback", DocumentDate = input.DocumentDate, CreatedByUser = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        // A late meeting CHECK failure occurs after the acta, sections and participant were saved inside the transaction.
        var meeting = new ScheduledMeeting { Modality = "VIRTUAL", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(-1), OrganizerUserId = 1 };
        await Assert.ThrowsAsync<DbUpdateException>(() => new ActaStore(db).CreateAsync(new NewActa(acta,
            [new() { TemplateVersionId = input.TemplateVersionId, TemplateSectionId = input.Sections![0].TemplateSectionId }],
            [new() { UserName = "X", Email = "rollback@example.invalid", ParticipantType = "EXTERNO" }], meeting, null, "simulated@example.invalid"), default));
        Assert.Empty(await db.Set<Acta>().ToArrayAsync()); Assert.Empty(await db.Set<ActaSection>().ToArrayAsync());
        Assert.Empty(await db.Set<Participant>().ToArrayAsync()); Assert.Empty(await db.Set<ActaParticipant>().ToArrayAsync());
    }
    internal static async Task AssertFailure(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        Assert.Equal(3, root.EnumerateObject().Count()); Assert.False(root.GetProperty("isSuccess").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("result").ValueKind);
    }
}
