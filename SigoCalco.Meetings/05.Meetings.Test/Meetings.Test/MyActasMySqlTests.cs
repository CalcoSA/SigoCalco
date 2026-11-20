using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Meetings.Test;

public sealed class MyActasMySqlFactAttribute : FactAttribute
{
    public MyActasMySqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEETINGS_APPROVED_ACTAS_MYSQL") != "1") Skip="Requiere MySQL local y una base efímera para migración y pruebas de Mis actas.";
    }
}
public sealed class MyActasMySqlTests
{
    private static MeetingsDbContext Context(string connection)=>new(new DbContextOptionsBuilder<MeetingsDbContext>()
        .UseMySql(connection,new MySqlServerVersion(new Version(8,0,46))).Options);

    [MyActasMySqlFact]
    public async Task Incremental_migration_preserves_history_and_management_is_atomic_on_mysql()
    {
        using var local=JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("MEETINGS_MYSQL_LOCAL_SETTINGS")!));
        var builder=new MySqlConnectionStringBuilder(local.RootElement.GetProperty("ConnectionStrings").GetProperty("MeetingsDatabase").GetString()!);
        Assert.Equal("127.0.0.1",builder.Server);Assert.Equal(3306u,builder.Port);Assert.Equal("bdcalco_sigocalco",builder.Database);
        await using var real=Context(builder.ConnectionString);
        Assert.Equal("6f5f45b3-9fc3-11f1-b008-b082e2815ab3",await real.Database.SqlQueryRaw<string>("SELECT @@server_uuid AS Value").SingleAsync());
        var before=await real.Database.SqlQueryRaw<string>("SELECT CONCAT(permission_key,':',permission_value) AS Value FROM sigocalco_meetings_permission ORDER BY permission_id").ToArrayAsync();
        var name="sigocalco_actas_test_"+Guid.NewGuid().ToString("N");
        Assert.Matches("^sigocalco_actas_test_[a-f0-9]{32}$",name);
        builder.Database=name;builder.Pooling=false;
        var connection=builder.ConnectionString;var created=false;
        try
        {
#pragma warning disable EF1002 // Generated GUID-only identifier, validated above; no user input in DDL identifiers.
            await real.Database.ExecuteSqlRawAsync($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
#pragma warning restore EF1002
            created=true;
            await using(var db=Context(connection))
            {
                foreach(var script in new[]{"02_create_model.sql","04_add_meeting_notifications.sql"})
                    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"sql",script)));
                // Historical fixture uses the old schema, including a notification tied only to its meeting.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO sigocalco_meetings_acta_type VALUES(1,'REUNION','Acta de reunión'),(3,'ADMINISTRATIVA','Acta administrativa');
                    INSERT INTO sigocalco_meetings_acta_status VALUES(1,'BORRADOR'),(2,'ABIERTA'),(3,'EN_REVISION'),(4,'CERRADA'),(5,'ANULADA');
                    INSERT INTO sigocalco_meetings_meeting_status VALUES(1,'PROGRAMADA'),(2,'FINALIZADA'),(3,'CANCELADA');
                    INSERT INTO sigocalco_meetings_template(template_id,acta_type_id,name,scope,created_by_user,created_at) VALUES(1,1,'Histórica','GLOBAL',1,'2026-11-01');
                    INSERT INTO sigocalco_meetings_template_version(template_version_id,template_id,acta_type_id,version_number,created_by_user,created_at) VALUES(1,1,1,1,1,'2026-11-01');
                    INSERT INTO sigocalco_meetings_acta(acta_id,acta_code,acta_type_id,template_version_id,solution_center_code,title,document_date,acta_status_id,created_by_user,created_at,updated_at) VALUES(1,'HISTORIC-FIXTURE',1,1,'R01','Historia', '2026-11-01',1,1,'2026-11-01','2026-11-01');
                    INSERT INTO sigocalco_meetings_participant(participant_id,userName,email,participant_type) VALUES(1,'Histórico','historical@example.invalid','EXTERNO');
                    INSERT INTO sigocalco_meetings_acta_participant VALUES(1,1,1);
                    INSERT INTO sigocalco_meetings_meeting(meeting_id,acta_id,modality,start_at_utc,end_at_utc,organizer_user_id,meeting_status_id,created_at,updated_at) VALUES(1,1,'VIRTUAL','2026-11-01 15:00','2026-11-01 16:00',1,1,'2026-11-01','2026-11-01');
                    INSERT INTO sigocalco_meetings_meeting_notification(meeting_notification_id,meeting_id,recipient_email,recipient_name,message_id,subject,text_body,html_body,status,attempts,created_at,updated_at) VALUES(1,1,'historical@example.invalid','Histórico','historical@sigocalco.local','Historia','Historia','Historia','ENVIADO',1,'2026-11-01','2026-11-01');
                    """);
                await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"sql","06_mis_actas_incremental.sql")));
                Assert.Equal(new[]{"ANULADA","BORRADOR","PUBLICADA"},await db.Set<ActaStatus>().OrderBy(s=>s.Name).Select(s=>s.Name).ToArrayAsync());
                Assert.Equal(1,(await db.Set<ActaStatus>().SingleAsync(s=>s.Name=="BORRADOR")).ActaStatusId);
                Assert.Equal(5,(await db.Set<ActaStatus>().SingleAsync(s=>s.Name=="ANULADA")).ActaStatusId);
                var historical=await db.Set<MeetingNotification>().SingleAsync();Assert.Equal(1,historical.ActaId);Assert.Equal("ENVIADO",historical.Status);Assert.Equal("Historia",historical.TextBody);
                Assert.Null((await db.Set<ActaParticipant>().SingleAsync()).Attended);
                db.AddRange(MeetingsPermissions.Keys.Select(k=>new MeetingsPermission{PermissionKey=k,PermissionValue="Administrador"}));await db.SaveChangesAsync();
            }
            using var factory=new Factory(connection);using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost")});
            client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",MeetingsApiFactory.Token());
            using var complete=await client.PostAsJsonAsync("/api/v1/templates/with-version",TemplateManagementTests.Request());
            Assert.Equal(HttpStatusCode.Created,complete.StatusCode);
            var template=(await complete.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
            var input=new CreateActaDto(template.TemplateVersionId,"Acta de prueba",new(2026,11,20),"R01",
                [new(template.Sections.Single().TemplateSectionId,JsonSerializer.Deserialize<JsonElement>("""{"fecha":"2026-11-20","hora":"10:00","lugar":"Prueba"}"""))],
                [new("Participante","test@example.invalid")],null);
            var acta=await MyActasTests.Create(client,input);
            Assert.Equal("SIMULADO",acta.Notifications!.State);
            var detail=(await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{acta.ActaId}"))!;
            var person=Assert.Single(detail.Participants).ParticipantId;
            using var attendance=await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}/participants/{person}/attendance",new {attended=true});Assert.Equal(HttpStatusCode.OK,attendance.StatusCode);
            using var update=await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}",new UpdateActaDto("Editada",input.DocumentDate,input.Sections));Assert.Equal(HttpStatusCode.OK,update.StatusCode);
            var responses=await Task.WhenAll(client.PatchAsJsonAsync($"/api/v1/actas/{acta.ActaId}/status",new {status="PUBLICADA"}),client.PatchAsJsonAsync($"/api/v1/actas/{acta.ActaId}/status",new {status="ANULADA"}));
            Assert.All(responses,r=>Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict));foreach(var r in responses)r.Dispose();
            detail=(await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{acta.ActaId}"))!;Assert.Equal("ANULADA",detail.Acta.Status);Assert.True(detail.Participants.Single().Attended);
            await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}",new UpdateActaDto("No guardar",input.DocumentDate,input.Sections)),HttpStatusCode.Conflict);
            foreach(var meeting in new[]{ActaApiTests.Meeting("VIRTUAL"),ActaApiTests.Meeting("PRESENCIAL") with {ReserveRoom=false,ExternalRoomId=null},ActaApiTests.Meeting("PRESENCIAL") with {ReserveRoom=true}})
            {
                var result=await MyActasTests.Create(client,input with {Meeting=meeting});
                var item=(await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{result.ActaId}"))!;
                Assert.Equal(meeting.ReserveRoom==true,item.Meeting!.Reservation is not null);
            }
            var listed=(await client.GetResultAsync<PagedActasDto>("/api/v1/actas?pageSize=2"))!;Assert.Equal(5,listed.TotalCount);Assert.Equal(3,listed.TotalPages);
            var available=(await client.GetResultAsync<PagedAvailableTemplatesDto>("/api/v1/actas/available-templates"))!;Assert.Equal(2,available.TotalCount);
            await using(var fresh=Context(connection))
            {
                Assert.Equal("HISTORIC-FIXTURE",(await fresh.Set<Acta>().SingleAsync(a=>a.ActaId==1)).ActaCode);
                Assert.Equal(5,await fresh.Set<MeetingNotification>().CountAsync());
                Assert.Equal(1,await fresh.Set<MeetingReservation>().CountAsync());
            }
        }
        finally
        {
            if(created)
            {
                if(!Regex.IsMatch(name,"^sigocalco_actas_test_[a-f0-9]{32}$"))throw new InvalidOperationException("Identificador temporal inesperado.");
#pragma warning disable EF1002 // Only the owned, generated and validated ephemeral database is removed.
                await real.Database.ExecuteSqlRawAsync($"DROP DATABASE `{name}`");
#pragma warning restore EF1002
            }
            Assert.Equal(before,await real.Database.SqlQueryRaw<string>("SELECT CONCAT(permission_key,':',permission_value) AS Value FROM sigocalco_meetings_permission ORDER BY permission_id").ToArrayAsync());
        }
    }
    private sealed class Factory(string connection):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");builder.ConfigureLogging(l=>l.ClearProviders());
            builder.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:MeetingsDatabase"]=connection,["Jwt:Secret"]=MeetingsApiFactory.Secret,
                ["Jwt:Issuer"]="meetings-tests",["Jwt:Audience"]="meetings-tests",["Smtp:Enabled"]="false"
            }));
            builder.ConfigureServices(s=>{s.RemoveAll<IAuthenticationDirectory>();s.AddScoped<IAuthenticationDirectory,StubDirectory>();});
        }
    }
}
