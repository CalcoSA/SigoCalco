using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class MyActasTests
{
    [Fact]
    public void Swagger_publishes_the_management_routes_and_standard_responses()
    {
        using var factory=new MeetingsApiFactory();using var client=factory.Client();
        var doc=factory.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        Assert.Equal(19,doc.Paths.Values.Sum(p=>p.Operations!.Count));
        foreach(var suffix in new[]{"","/{actaId}","/{actaId}/participants","/{actaId}/participants/{participantId}/attendance","/{actaId}/status","/available-templates"})
            foreach(var operation in doc.Paths["/api/v1/actas"+suffix].Operations!.Values)
            {
                Assert.True(operation.Responses!.ContainsKey("401"));Assert.True(operation.Responses.ContainsKey("403"));
                Assert.True(operation.Responses.ContainsKey("409"));
            }
    }
    internal static async Task<CreateActaDto> Prepare(MeetingsApiFactory factory, HttpClient client)
    {
        var input = await ActaApiTests.PrepareAsync(factory, client);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        db.AddRange(new ActaStatus { Name = "PUBLICADA" }, new ActaStatus { Name = "ANULADA" }); await db.SaveChangesAsync();
        return input;
    }
    internal static async Task<CreatedActaDto> Create(HttpClient client, CreateActaDto input)
    {
        using var response = await ActaApiTests.SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = (await response.Content.ReadResultAsync<CreatedActaDto>())!;
        Assert.Equal("BORRADOR", result.Status); return result;
    }

    [Fact]
    public async Task List_is_paged_by_owner_and_detail_uses_the_original_version()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input = await Prepare(factory, client);
        var first = await Create(client, input);
        var second = await Create(client, input with { Title = "Segunda", Meeting = ActaApiTests.Meeting("VIRTUAL") });
        var foreign = await Create(client, input with { Title = "Privada" });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MeetingsDbContext>().Set<Acta>().Where(a => a.ActaId == foreign.ActaId).ExecuteUpdateAsync(s => s.SetProperty(a => a.CreatedByUser, 2));
        var page = (await client.GetResultAsync<PagedActasDto>("/api/v1/actas?page=1&pageSize=1"))!;
        Assert.Equal(2, page.TotalCount); Assert.Equal(2, page.TotalPages); Assert.Equal(second.ActaId, Assert.Single(page.Items).ActaId);
        Assert.True(page.Items[0].HasMeeting); Assert.Equal("VIRTUAL", page.Items[0].Modality);
        var page2 = (await client.GetResultAsync<PagedActasDto>("/api/v1/actas?page=2&pageSize=1"))!;
        Assert.False(Assert.Single(page2.Items).HasMeeting);
        var detail = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{first.ActaId}"))!;
        Assert.Equal(input.TemplateVersionId, detail.TemplateVersionId); Assert.Equal("BORRADOR", detail.Acta.Status);
        Assert.Equal("FORMULARIO", Assert.Single(detail.Sections).ContentType); Assert.NotNull(detail.Sections[0].ContentJson);
        Assert.Null(Assert.Single(detail.Participants).Attended); Assert.Null(detail.Meeting);
        using var version = await client.PostAsJsonAsync($"/api/v1/templates/{detail.TemplateId}/versions", new CreateTemplateVersionDto([new("Nueva", "TEXTO", 1, false, null)]));
        Assert.Equal(HttpStatusCode.Created, version.StatusCode);
        var stillOriginal = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{first.ActaId}"))!;
        Assert.Equal(detail.TemplateVersionId, stillOriginal.TemplateVersionId); Assert.Equal("Información", stillOriginal.Sections[0].Title);
        var available = (await client.GetResultAsync<PagedAvailableTemplatesDto>("/api/v1/actas/available-templates"))!;
        var latest = Assert.Single(available.Items); Assert.Equal(2, latest.VersionNumber); Assert.Equal("Nueva", Assert.Single(latest.Sections).Title);
        using var empty = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1, "Sin versiones", "GLOBAL", null));
        Assert.Equal(HttpStatusCode.Created, empty.StatusCode);
        available = (await client.GetResultAsync<PagedAvailableTemplatesDto>("/api/v1/actas/available-templates"))!;
        var noVersion = Assert.Single(available.Items, t => !t.CanCreateActa); Assert.Null(noVersion.TemplateVersionId); Assert.Empty(noVersion.Sections);
        await ActaApiTests.AssertFailure(await client.GetAsync($"/api/v1/actas/{foreign.ActaId}"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("BORRADOR", "PUBLICADA", 200)] [InlineData("BORRADOR", "ANULADA", 200)] [InlineData("PUBLICADA", "ANULADA", 200)]
    [InlineData("PUBLICADA", "BORRADOR", 409)] [InlineData("ANULADA", "BORRADOR", 409)] [InlineData("ANULADA", "PUBLICADA", 409)]
    [InlineData("BORRADOR", "BORRADOR", 409)] [InlineData("PUBLICADA", "PUBLICADA", 409)] [InlineData("ANULADA", "ANULADA", 409)]
    [InlineData("BORRADOR", "CERRADA", 400)]
    public async Task Transitions_are_restricted_and_preserve_related_content(string initial, string next, int expected)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input = await Prepare(factory, client);
        var created = await Create(client, input);
        await SetStatus(factory, created.ActaId, initial);
        var before = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{created.ActaId}"))!;
        using var response = await client.PatchAsJsonAsync($"/api/v1/actas/{created.ActaId}/status", new { status = next });
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
        var after = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{created.ActaId}"))!;
        Assert.Equal(expected == 200 ? next : initial, after.Acta.Status);
        Assert.Equal(before.TemplateVersionId, after.TemplateVersionId);
        Assert.Equal(JsonSerializer.Serialize(before.Sections), JsonSerializer.Serialize(after.Sections));
        Assert.Equal(before.Participants, after.Participants);
    }

    [Fact]
    public async Task Draft_updates_validate_json_and_protect_identifiers_and_state()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input = await Prepare(factory, client);
        var created = await Create(client, input);
        using var updated = await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}", new UpdateActaDto("Título editado", input.DocumentDate.AddDays(1), input.Sections));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var detail = (await updated.Content.ReadResultAsync<ActaDetailDto>())!;
        Assert.Equal("Título editado", detail.Acta.Title); Assert.Equal(created.ActaCode, detail.Acta.ActaCode);
        Assert.Equal("BORRADOR", detail.Acta.Status); Assert.Equal(1, detail.Acta.CreatedByUser);
        using var invalid = await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}", new UpdateActaDto("No guardar", input.DocumentDate, []));
        await ActaApiTests.AssertFailure(invalid, HttpStatusCode.BadRequest);
        using var forbiddenField = await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}", new { title="No guardar", documentDate=input.DocumentDate, sections=input.Sections, status="PUBLICADA", templateVersionId=999 });
        await ActaApiTests.AssertFailure(forbiddenField, HttpStatusCode.BadRequest);
        using var createState = await client.PostAsJsonAsync("/api/v1/actas", new { input.TemplateVersionId, input.Title, input.DocumentDate, input.SolutionCenterCode, input.Sections, input.Participants, status="PUBLICADA" });
        await ActaApiTests.AssertFailure(createState, HttpStatusCode.BadRequest);
        Assert.Equal("Título editado", (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{created.ActaId}"))!.Acta.Title);
    }

    [Fact]
    public async Task Participant_replacement_and_attendance_are_scoped_and_do_not_send_more_emails()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input = await Prepare(factory, client);
        var one = await Create(client, input); var two = await Create(client, input);
        var detail = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{one.ActaId}"))!;
        var person = Assert.Single(detail.Participants).ParticipantId;
        foreach (var attendance in new bool?[] { true, false, null })
        {
            using var response = await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants/{person}/attendance", new { attended=attendance });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(attendance, Assert.Single((await response.Content.ReadResultAsync<ActaDetailDto>())!.Participants).Attended);
            Assert.Null(Assert.Single((await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{two.ActaId}"))!.Participants).Attended);
        }
        using (var marked = await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants/{person}/attendance", new { attended=true })) Assert.Equal(HttpStatusCode.OK, marked.StatusCode);
        using (var replace = await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants", new { participants = new[] { input.Participants![0], new ParticipantInput("Otra", "other@example.invalid") } }))
        {
            Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
            var result = (await replace.Content.ReadResultAsync<ActaDetailDto>())!;
            Assert.Equal(2, result.Participants.Count); Assert.True(result.Participants.Single(p => p.ParticipantId == person).Attended);
        }
        using (var duplicate = await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants", new { participants=new[] {input.Participants![0],input.Participants[0]} }))
            await ActaApiTests.AssertFailure(duplicate, HttpStatusCode.BadRequest);
        using (var empty = await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants", new { participants=Array.Empty<ParticipantInput>() })) Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{one.ActaId}/participants/{person}/attendance", new { attended=true }), HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{two.ActaId}/participants/{person}/attendance", new { }), HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(2, await db.Set<MeetingNotification>().CountAsync());
        Assert.Equal(2, await db.Set<Participant>().CountAsync()); Assert.Single(await db.Set<ActaParticipant>().ToArrayAsync());
    }

    [Theory]
    [InlineData("PUBLICADA")] [InlineData("ANULADA")]
    public async Task Content_participants_and_attendance_cannot_be_edited_after_draft(string status)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input = await Prepare(factory, client);
        var created = await Create(client, input); await SetStatus(factory, created.ActaId, status);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}", new UpdateActaDto("No", input.DocumentDate, input.Sections)), HttpStatusCode.Conflict);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}/participants", new { participants=Array.Empty<ParticipantInput>() }), HttpStatusCode.Conflict);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{created.ActaId}/participants/1/attendance", new { attended=true }), HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("GET", "")] [InlineData("GET", "/1")] [InlineData("GET", "/available-templates")]
    [InlineData("PUT", "/1")] [InlineData("PUT", "/1/participants")] [InlineData("PUT", "/1/participants/1/attendance")] [InlineData("PATCH", "/1/status")]
    public async Task All_management_routes_enforce_jwt_and_permission(string method, string suffix)
    {
        using var factory = new MeetingsApiFactory(); using var anonymous = factory.Client(false);
        object body = suffix.EndsWith("attendance") ? new { attended=(bool?)true } : suffix.EndsWith("status") ? new { status="PUBLICADA" }
            : suffix.EndsWith("participants") ? new { participants=Array.Empty<ParticipantInput>() } : new UpdateActaDto("X", new(2026,11,20), []);
        using var request = new HttpRequestMessage(new(method), "/api/v1/actas"+suffix) { Content=JsonContent.Create(body) };
        await ActaApiTests.AssertFailure(await anonymous.SendAsync(request), HttpStatusCode.Unauthorized);
        using var authenticated = factory.Client();
        using var denied = new HttpRequestMessage(new(method), "/api/v1/actas"+suffix) { Content=JsonContent.Create(body) };
        await ActaApiTests.AssertFailure(await authenticated.SendAsync(denied), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task All_mutations_hide_foreign_actas_even_with_same_center()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); var input=await Prepare(factory,client); var acta=await Create(client,input);
        using (var scope=factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MeetingsDbContext>().Set<Acta>().Where(a=>a.ActaId==acta.ActaId).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.CreatedByUser,2));
        var root=$"/api/v1/actas/{acta.ActaId}";
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root,new UpdateActaDto("No",input.DocumentDate,input.Sections)),HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root+"/participants",new {participants=input.Participants}),HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync(root+"/participants/1/attendance",new {attended=true}),HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.PatchAsJsonAsync(root+"/status",new {status="ANULADA"}),HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)] [InlineData(null)]
    public async Task Presential_reservation_is_optional_and_legacy_room_requests_still_work(bool? reserve)
    {
        var provider=new RecordingReservation(); using var factory=new MeetingsApiFactory {Customize=s=>{s.RemoveAll<IRoomReservationProvider>();s.AddSingleton<IRoomReservationProvider>(provider);}};
        using var client=factory.Client(); var input=await Prepare(factory,client);
        var shouldReserve=reserve != false;
        var meeting=ActaApiTests.Meeting("PRESENCIAL") with { ReserveRoom=reserve, ExternalRoomId=shouldReserve?"ROOM-1":null };
        var acta=await Create(client,input with {Meeting=meeting});
        Assert.Equal(shouldReserve?1:0,provider.Calls);
        var detail=(await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{acta.ActaId}"))!;
        Assert.NotNull(detail.Meeting); Assert.Equal(shouldReserve,detail.Meeting.Reservation is not null);
        using var scope=factory.Services.CreateScope();
        Assert.Equal(shouldReserve?1:0,await scope.ServiceProvider.GetRequiredService<MeetingsDbContext>().Set<MeetingReservation>().CountAsync());
        if(!shouldReserve) Assert.Null(acta.Integration);
    }

    [Theory]
    [InlineData("PRESENCIAL",true,null)] [InlineData("VIRTUAL",true,"ROOM-1")] [InlineData("PRESENCIAL",false,"ROOM-1")]
    public async Task Contradictory_reservation_requests_are_rejected(string modality,bool reserve,string? room)
    {
        using var factory=new MeetingsApiFactory();using var client=factory.Client();var input=await Prepare(factory,client);
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client,input with { Meeting=ActaApiTests.Meeting(modality) with {ReserveRoom=reserve,ExternalRoomId=room}}),HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task No_recipients_is_recorded_without_adding_the_organizer_as_recipient()
    {
        using var factory=new MeetingsApiFactory();using var client=factory.Client();var input=await Prepare(factory,client);
        var acta=await Create(client,input with {Participants=[]});
        Assert.Equal("SIN_DESTINATARIOS",acta.Notifications!.State);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Empty(await db.Set<MeetingNotification>().ToArrayAsync());
        Assert.Single(await db.Set<MeetingsLog>().Where(l=>l.Action=="ACTA_SIN_DESTINATARIOS").ToArrayAsync());
    }

    private static async Task SetStatus(MeetingsApiFactory factory,int id,string status)
    {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var statusId=await db.Set<ActaStatus>().Where(s=>s.Name==status).Select(s=>s.ActaStatusId).SingleAsync();
        await db.Set<Acta>().Where(a=>a.ActaId==id).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.ActaStatusId,statusId));
    }

    [Fact]
    public async Task Available_templates_and_creation_cannot_bypass_template_visibility_by_id()
    {
        using var factory=new MeetingsApiFactory();using var client=factory.Client();var input=await Prepare(factory,client);
        await TemplateManagementTests.SetPermissions(factory,MeetingsPermissions.ActasManage);
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/actas/available-templates"),HttpStatusCode.Forbidden);
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client,input),HttpStatusCode.Forbidden);
        await TemplateManagementTests.SetPermissions(factory,MeetingsPermissions.ActasManage,MeetingsPermissions.GlobalRead);
        Assert.Single((await client.GetResultAsync<PagedAvailableTemplatesDto>("/api/v1/actas/available-templates"))!.Items);
        await Create(client,input);
    }

    [Fact]
    public async Task Failed_smtp_without_meeting_keeps_acta_and_durable_error_without_duplicates()
    {
        var sender=new FailingSender();using var factory=new MeetingsApiFactory { Customize=s=>{s.RemoveAll<IMeetingNotificationSender>();s.AddSingleton<IMeetingNotificationSender>(sender);} };
        using var client=factory.Client();var input=await Prepare(factory,client);var key=Guid.NewGuid().ToString();
        using var response=await ActaApiTests.SendAsync(client,input,key);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var result=(await response.Content.ReadResultAsync<CreatedActaDto>())!;Assert.Equal("ERROR",result.Notifications!.State);Assert.Null(result.MeetingId);
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client,input,key),HttpStatusCode.Conflict);
        Assert.Equal(1,sender.Calls);
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Single(await db.Set<Acta>().ToArrayAsync());Assert.Equal("ERROR",(await db.Set<MeetingNotification>().SingleAsync()).Status);
    }
    private sealed class FailingSender : IMeetingNotificationSender
    {
        public int Calls;
        public Task<NotificationReceipt> SendAsync(MeetingNotification n,CancellationToken ct){Calls++;throw new NotificationFailure("Fallo simulado.");}
    }
    private sealed class RecordingReservation : IRoomReservationProvider
    {
        public int Calls;
        public Task<ProviderResult> ReserveAsync(MeetingIntegrationRequest request,CancellationToken ct)
        { Calls++;return Task.FromResult(new ProviderResult("SIMULATED:test",true,false)); }
    }
}
