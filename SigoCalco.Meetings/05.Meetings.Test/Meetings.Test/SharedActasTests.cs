using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class SharedActasTests
{
    internal const string Shared = "/api/v1/actas/shared-with-me";
    internal sealed class Directory : IAuthenticationDirectory
    {
        public Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(["Administrador", "Lector de prueba"]);
        public Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct)
        {
            var expectedRole = userId == 2 ? "Lector de prueba" : "Administrador";
            if (userId is < 1 or > 3 || roleId != 1 || roleName != expectedRole) throw MeetingsRequestException.Forbidden();
            return Task.FromResult(new MeetingsIdentity(userId, [expectedRole], ["R01"], "Nombre idéntico", "ignored@example.invalid",
                userId switch { 1 => "test.admin", 2 => "alice.login", _ => "stranger" }));
        }
    }
    internal static void Authenticate(HttpClient client, int userId, string? login = null) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MeetingsApiFactory.Token(
            idUser: userId.ToString(), sub: userId.ToString(), role: userId == 2 ? "Lector de prueba" : "Administrador",
            login: login ?? (userId switch { 1 => "test.admin", 2 => "alice.login", _ => "stranger" })));

    private static MeetingsApiFactory Factory() => new() { Customize = s =>
    {
        s.RemoveAll<IAuthenticationDirectory>(); s.AddScoped<IAuthenticationDirectory, Directory>();
    } };
    internal static ParticipantInput Internal(string email = "alice@example.invalid", string login = "alice.login") =>
        new("Nombre idéntico", email, "INTERNO", login);

    // Used unchanged by SQLite and real isolated MySQL: SQL paging, access checks and duplicate elimination.
    internal static async Task ExerciseReads(IServiceProvider services, HttpClient client, CreateActaDto input)
    {
        Authenticate(client, 1);
        var one = await MyActasTests.Create(client, input with { Participants = [Internal(), Internal("alice.second@example.invalid", " ALICE.LOGIN ")] });
        var two = await MyActasTests.Create(client, input with { Participants = [Internal()], Meeting = ActaApiTests.Meeting("VIRTUAL") });
        var three = await MyActasTests.Create(client, input with { Participants = [Internal()] });
        var own = await MyActasTests.Create(client, input with { Participants = [Internal()] });
        var foreign = await MyActasTests.Create(client, input with { Participants = [Internal("unrelated@example.invalid", "alice.login.other")] });
        var external = await MyActasTests.Create(client, input with { Participants = [new("Nombre idéntico", "external@example.invalid")] });
        var malformed = await MyActasTests.Create(client, input with { Participants = [new("Nombre idéntico", "malformed@example.invalid")] });
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            var states = await db.Set<ActaStatus>().ToDictionaryAsync(s => s.Name, s => s.ActaStatusId);
            var actas = await db.Set<Acta>().ToDictionaryAsync(a => a.ActaId);
            // Deliberately reverse creation time vs ID and tie two times to test both sort criteria.
            actas[one.ActaId].CreatedAt = new(2026, 11, 23, 12, 0, 0);
            actas[two.ActaId].CreatedAt = new(2026, 11, 23, 11, 0, 0);
            actas[three.ActaId].CreatedAt = actas[two.ActaId].CreatedAt;
            actas[two.ActaId].SolutionCenterCode = "OTHER-CENTER";
            actas[two.ActaId].ActaStatusId = states["PUBLICADA"];
            actas[three.ActaId].ActaStatusId = states["ANULADA"];
            actas[own.ActaId].CreatedByUser = 2;
            // Historical malformed external row: it must never grant internal access.
            (await db.Set<Participant>().SingleAsync(p => p.Email == "malformed@example.invalid")).UserLogin = "alice.login";
            await db.SaveChangesAsync();
        }
        var ownPage = (await client.GetResultAsync<PagedActasDto>("/api/v1/actas"))!;
        Assert.Contains(ownPage.Items, a => a.ActaId == one.ActaId);
        Assert.DoesNotContain(ownPage.Items, a => a.ActaId == own.ActaId);
        Assert.Empty((await client.GetResultAsync<PagedActasDto>(Shared))!.Items);
        var original = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{one.ActaId}"))!;
        using (var next = await client.PostAsJsonAsync($"/api/v1/templates/{original.TemplateId}/versions",
            new CreateTemplateVersionDto([new("Siguiente", "TEXTO", 1, false, null)]))) Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Authenticate(client, 2, " ALICE.LOGIN ");
        var page = (await client.GetResultAsync<PagedActasDto>(Shared + "?pageSize=2"))!;
        Assert.Equal(3, page.TotalCount); Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { one.ActaId, three.ActaId }, page.Items.Select(a => a.ActaId));
        var second = (await client.GetResultAsync<PagedActasDto>(Shared + "?page=2&pageSize=2"))!;
        Assert.Equal(two.ActaId, Assert.Single(second.Items).ActaId); Assert.Equal(3, second.TotalCount);
        Assert.Empty((await client.GetResultAsync<PagedActasDto>(Shared + "?page=3&pageSize=2"))!.Items);
        var all = (await client.GetResultAsync<PagedActasDto>(Shared))!;
        Assert.Equal(new[] { "BORRADOR", "ANULADA", "PUBLICADA" }, all.Items.Select(a => a.Status));
        foreach (var actaId in new[] { one.ActaId, two.ActaId, three.ActaId })
        {
            var detail = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{actaId}"))!;
            Assert.Equal(input.TemplateVersionId, detail.TemplateVersionId); Assert.Equal(1, detail.VersionNumber);
            Assert.NotEmpty(detail.Participants); Assert.NotNull(Assert.Single(detail.Sections).ContentJson);
            Assert.Equal(actaId == two.ActaId, detail.Meeting is not null);
        }
        foreach (var id in new[] { foreign.ActaId, external.ActaId, malformed.ActaId, int.MaxValue })
            await ActaApiTests.AssertFailure(await client.GetAsync($"/api/v1/actas/{id}"), HttpStatusCode.NotFound);
        // Being the creator AND a participant still requires the owner's functional permission.
        await ActaApiTests.AssertFailure(await client.GetAsync($"/api/v1/actas/{own.ActaId}"), HttpStatusCode.Forbidden);
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/actas"), HttpStatusCode.Forbidden);
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            // Isolated fixture grant only; proves shared access does not change with functional grants.
            (await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == MeetingsPermissions.ActasManage)).PermissionValue += ",Lector de prueba";
            await db.SaveChangesAsync();
        }
        Assert.Equal(own.ActaId, Assert.Single((await client.GetResultAsync<PagedActasDto>("/api/v1/actas"))!.Items).ActaId);
        Assert.Equal(3, (await client.GetResultAsync<PagedActasDto>(Shared))!.TotalCount);
        Authenticate(client, 3); // Administrator, same center, still not a participant.
        Assert.Empty((await client.GetResultAsync<PagedActasDto>(Shared))!.Items);
        await ActaApiTests.AssertFailure(await client.GetAsync($"/api/v1/actas/{one.ActaId}"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_and_detail_follow_document_participation_in_all_states()
    {
        using var factory = Factory(); using var client = factory.Client();
        var input = await MyActasTests.Prepare(factory, client);
        await ExerciseReads(factory.Services, client, input);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Participant_cannot_mutate_even_with_functional_permission_and_access_disappears_when_removed(bool grant)
    {
        using var factory = Factory(); using var client = factory.Client(); var input = await MyActasTests.Prepare(factory, client);
        var created = await MyActasTests.Create(client, input with { Participants = [Internal()] });
        if (grant)
        {
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            (await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == MeetingsPermissions.ActasManage)).PermissionValue += ",Lector de prueba";
            await db.SaveChangesAsync();
        }
        Authenticate(client, 2);
        var root = $"/api/v1/actas/{created.ActaId}";
        var detail = (await client.GetResultAsync<ActaDetailDto>(root))!;
        var expected = grant ? HttpStatusCode.NotFound : HttpStatusCode.Forbidden;
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root, new UpdateActaDto("No", input.DocumentDate, input.Sections)), expected);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root + "/participants", new { participants = Array.Empty<ParticipantInput>() }), expected);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root + $"/participants/{detail.Participants[0].ParticipantId}/attendance", new { attended = true }), expected);
        foreach (var status in new[] { "PUBLICADA", "ANULADA" })
            await ActaApiTests.AssertFailure(await client.PatchAsJsonAsync(root + "/status", new { status }), expected);
        Authenticate(client, 1);
        using var remove = await client.PutAsJsonAsync(root + "/participants", new { participants = Array.Empty<ParticipantInput>() });
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Authenticate(client, 2);
        Assert.Empty((await client.GetResultAsync<PagedActasDto>(Shared))!.Items);
        await ActaApiTests.AssertFailure(await client.GetAsync(root), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("missing")] [InlineData("invalid")] [InlineData("expired")]
    [InlineData("noLogin")] [InlineData("blankLogin")] [InlineData("wrongLogin")]
    public async Task Shared_reads_reject_invalid_identity(string mode)
    {
        using var factory = Factory(); using var client = factory.Client(false);
        if (mode != "missing") client.DefaultRequestHeaders.Authorization = new("Bearer", mode switch
        {
            "invalid" => "invalid.token", "expired" => MeetingsApiFactory.Token(expired: true),
            "noLogin" => MeetingsApiFactory.Token(login: null), "blankLogin" => MeetingsApiFactory.Token(login: " "),
            _ => MeetingsApiFactory.Token(login: "alice.login")
        });
        foreach (var url in new[] { Shared, "/api/v1/actas/1" })
            await ActaApiTests.AssertFailure(await client.GetAsync(url), HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("page=0")] [InlineData("pageSize=0")] [InlineData("pageSize=101")]
    [InlineData("page=2147483647&pageSize=100")] [InlineData("page=abc")]
    public async Task Pagination_limits_are_preserved(string query)
    {
        using var factory = Factory(); using var client = factory.Client();
        await ActaApiTests.AssertFailure(await client.GetAsync(Shared + "?" + query), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("userLogin=alice.login")] [InlineData("userId=2")] [InlineData("createdByUser=2")]
    [InlineData("idUser=2")]
    public async Task Read_routes_reject_identity_parameters_and_bodies(string query)
    {
        using var factory = Factory(); using var client = factory.Client();
        foreach (var url in new[] { Shared, "/api/v1/actas/1" })
        {
            await ActaApiTests.AssertFailure(await client.GetAsync(url + "?" + query), HttpStatusCode.BadRequest);
            using var request = new HttpRequestMessage(HttpMethod.Get, url) { Content = JsonContent.Create(new { userLogin = "alice.login" }) };
            await ActaApiTests.AssertFailure(await client.SendAsync(request), HttpStatusCode.BadRequest);
        }
    }

    [Theory]
    [InlineData("EXTERNO", "alice.login")]
    [InlineData("INTERNO", null)] [InlineData("INTERNO", " ")] [InlineData("INTERNO", "alice\nlogin")]
    public async Task Invalid_participant_identity_is_rejected_on_create_and_replace(string type, string? login)
    {
        using var factory = Factory(); using var client = factory.Client(); var input = await MyActasTests.Prepare(factory, client);
        var created = await MyActasTests.Create(client, input);
        var participants = new[] { new ParticipantInput("Nombre", "new@example.invalid", type, login) };
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input with { Participants = participants }), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}/participants", new { participants }), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("INTERNO", "stranger")] [InlineData("EXTERNO", null)]
    public async Task Reusing_email_cannot_silently_link_a_different_identity(string type, string? login)
    {
        using var factory = Factory(); using var client = factory.Client(); var input = await MyActasTests.Prepare(factory, client);
        var created = await MyActasTests.Create(client, input with { Participants = [Internal()] });
        var participants = new[] { new ParticipantInput("Nombre", "alice@example.invalid", type, login) };
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input with { Participants = participants }), HttpStatusCode.Conflict);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}/participants", new { participants }), HttpStatusCode.Conflict);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal("alice.login", (await db.Set<Participant>().SingleAsync()).UserLogin);
        Assert.Equal(1, await db.Set<Acta>().CountAsync()); Assert.Equal(1, await db.Set<ActaParticipant>().CountAsync());
    }

    [Fact]
    public async Task Reads_are_paged_in_sql_and_never_send_notifications_or_call_reservations()
    {
        var effects = new SideEffects();
        using var factory = new MeetingsApiFactory { Customize = s =>
        {
            s.RemoveAll<IAuthenticationDirectory>(); s.AddScoped<IAuthenticationDirectory, Directory>();
            s.RemoveAll<IMeetingNotificationSender>(); s.AddSingleton<IMeetingNotificationSender>(effects);
            s.RemoveAll<IRoomReservationProvider>(); s.AddSingleton<IRoomReservationProvider>(effects);
        } };
        using var client = factory.Client(); var input = await MyActasTests.Prepare(factory, client);
        var created = await MyActasTests.Create(client, input with { Participants = [Internal()], Meeting = ActaApiTests.Meeting("PRESENCIAL") });
        Assert.Equal(1, effects.Sends); Assert.Equal(1, effects.Reservations);
        Authenticate(client, 2); factory.Commands.Reset();
        Assert.Single((await client.GetResultAsync<PagedActasDto>(Shared + "?pageSize=1"))!.Items);
        Assert.Equal(2, factory.Commands.Count); // count + a single paged projection, no in-memory filtering/N+1.
        Assert.Contains(factory.Commands.Sql, sql => sql.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(factory.Commands.Sql, sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase) && sql.Contains("created_at") && sql.Contains("ORDER BY"));
        var detail = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{created.ActaId}"))!;
        Assert.NotNull(detail.Meeting!.Reservation); Assert.True(detail.Meeting.Reservation.Simulated);
        Assert.Equal(1, effects.Sends); Assert.Equal(1, effects.Reservations);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(1, await db.Set<MeetingNotification>().CountAsync());
    }

    private sealed class SideEffects : IMeetingNotificationSender, IRoomReservationProvider
    {
        public int Sends, Reservations;
        public Task<NotificationReceipt> SendAsync(MeetingNotification notification, CancellationToken ct)
        { Sends++; return Task.FromResult(new NotificationReceipt(true)); }
        public Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request, CancellationToken ct)
        { Reservations++; return Task.FromResult(new ProviderResult("SIMULATED:fixture", true)); }
    }
}
