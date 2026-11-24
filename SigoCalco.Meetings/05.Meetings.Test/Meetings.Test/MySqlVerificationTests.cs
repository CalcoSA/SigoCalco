using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Meetings.Infrastructure.Persistence.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;

namespace Meetings.Test;

public sealed class ApprovedMySqlFactAttribute : FactAttribute
{
    public ApprovedMySqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEETINGS_APPROVED_MYSQL_VERIFICATION") != "1")
            Skip = "Requiere autorización para la prueba MySQL con escrituras temporales.";
    }
}

public sealed class MySqlApiFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
        // Real production DbContext, JWT handler and AuthenticationDirectory; no persistence stubs.
    }
}

public sealed class MySqlVerificationTests
{
    // Controlled local fixtures: unique names/IDs, simulated notifications, and cleanup restricted to those fixtures.
    [ApprovedMySqlFact]
    public async Task Concurrent_versions_use_distinct_numbers_and_persist_their_sections()
    {
        var path = Environment.GetEnvironmentVariable("MEETINGS_MYSQL_LOCAL_SETTINGS")
            ?? throw new InvalidOperationException("Indique el archivo local de configuración, nunca contraseñas en argumentos.");
        using var local = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var connection = local.RootElement.GetProperty("ConnectionStrings").GetProperty("MeetingsDatabase").GetString()!;
        var connectionBuilder = new MySqlConnector.MySqlConnectionStringBuilder(connection);
        Assert.Equal("bdcalco_sigocalco", connectionBuilder.Database);
        Assert.Equal("127.0.0.1", connectionBuilder.Server);
        Assert.Equal(3306u, connectionBuilder.Port);
        var options = new DbContextOptionsBuilder<MeetingsDbContext>()
            .UseMySql(connection, new MySqlServerVersion(new Version(8,0,46))).Options;
        await using var db = new MeetingsDbContext(options);
        Assert.Equal("6f5f45b3-9fc3-11f1-b008-b082e2815ab3",
            await db.Database.SqlQueryRaw<string>("SELECT @@server_uuid AS Value").SingleAsync());
        await VerifyActualSchema(db);

        var authApi = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "../../../SigoCalco.Authentication/01.Authentication.Api/Authentication.Api"));
        var authConfig = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(authApi, "appsettings.json"))
            .AddJsonFile(Path.Combine(authApi, "appsettings.Development.json"), optional: true)
            .AddEnvironmentVariables().Build();
        foreach (var key in new[] { "Secret", "Issuer", "Audience" })
            Assert.True(local.RootElement.GetProperty("Jwt").GetProperty(key).GetString() == authConfig["Jwt:" + key],
                $"La configuración local Jwt:{key} no coincide con Authentication."); // Never print secret values.

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MeetingsDatabase"] = connection,
            ["Jwt:Secret"] = MeetingsApiFactory.Secret, // Isolated signing key for automated verification.
            ["Jwt:Issuer"] = authConfig["Jwt:Issuer"], ["Jwt:Audience"] = authConfig["Jwt:Audience"],
            ["Smtp:Enabled"] = "false",
            ["Logging:LogLevel:Default"] = "Warning"
        };
        var actor = await db.Database.SqlQueryRaw<VerificationActor>("""
            SELECT ur.IdUser, r.IdRole, r.nameRole AS NameRole
            FROM sigocalco_authentication_userrole ur
            JOIN sigocalco_authentication_users u ON u.IdUser = ur.IdUser AND u.statusUser = 1
            JOIN sigocalco_authentication_role r ON r.IdRole = ur.IdRole AND r.statusRole = 1
            WHERE UPPER(TRIM(r.nameRole)) = 'ADMINISTRADOR'
            ORDER BY ur.IdUser LIMIT 1
            """).SingleAsync();
        var issuer = new JwtTokenApplication(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        var verifiedActor = await new AuthenticationDirectory(db).ResolveAsync(actor.IdUser, actor.IdRole, actor.NameRole, default);
        Assert.False(string.IsNullOrWhiteSpace(verifiedActor.UserLogin));
        var token = issuer.GenerateToken(new UserDto
        {
            IdUser = actor.IdUser, IdRole = actor.IdRole, NameRole = actor.NameRole, UserLogin = verifiedActor.UserLogin
        }, Guid.NewGuid().ToString("D"));
        using var factory = new MySqlApiFactory(settings);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var types = await client.GetResultAsync<ActaTypeDto[]>("/api/v1/acta-types");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/actas/shared-with-me")).StatusCode);
        Assert.Equal(2, types!.Length);
        var type = await db.Set<ActaType>().SingleAsync(t => t.Code == "REUNION");
        var name = "CODEX-MIGRATION-VERIFY-" + Guid.NewGuid().ToString("N");
        var id = 0;
        var completeId = 0;
        try
        {
            using var created = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(type.ActaTypeId, name, "GLOBAL", null));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            id = (await created.Content.ReadResultAsync<CreatedTemplateDto>())!.TemplateId;
            var request = new CreateTemplateVersionDto([new("Verificación temporal", "TEXTO", 1, false, null)]);
            var responses = await Task.WhenAll(
                client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", request),
                client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", request));
            foreach (var response in responses) Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var versions = await Task.WhenAll(responses.Select(r => r.Content.ReadResultAsync<TemplateVersionDto>()));
            foreach (var response in responses) response.Dispose();
            Assert.Equal(new[] { 1, 2 }, versions.Select(v => v!.VersionNumber).OrderBy(n => n));
            Assert.Equal(2, await db.Set<TemplateVersion>().CountAsync(v => v.TemplateId == id));
            Assert.Equal(2, await db.Set<TemplateSection>().CountAsync(s =>
                db.Set<TemplateVersion>().Any(v => v.TemplateId == id && v.TemplateVersionId == s.TemplateVersionId)));
            var page = await client.GetResultAsync<PagedTemplatesDto>($"/api/v1/templates?actaTypeId={type.ActaTypeId}&scope=GLOBAL&pageSize=100");
            if (page!.TotalPages > 1)
                page = await client.GetResultAsync<PagedTemplatesDto>($"/api/v1/templates?actaTypeId={type.ActaTypeId}&scope=GLOBAL&pageSize=100&page={page.TotalPages}");
            var item = Assert.Single(page!.Items, t => t.TemplateId == id);
            Assert.Equal(actor.IdUser, item.CreatedByUser);
            Assert.Equal(new[] { 1, 2 }, item.Versions.Select(v => v.VersionNumber));
            Assert.All(item.Versions, v => Assert.Single(v.Sections));
            using var completeResponse = await client.PostAsJsonAsync("/api/v1/templates/with-version",
                TemplateManagementTests.Request() with { ActaTypeId = type.ActaTypeId, Name = name + "-complete" });
            Assert.Equal(HttpStatusCode.Created, completeResponse.StatusCode);
            var complete = (await completeResponse.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
            completeId = complete.TemplateId;
            Assert.Equal(1, complete.VersionNumber);
            var listedVersions = (await client.GetResultAsync<TemplateVersionDto[]>($"/api/v1/templates/{id}/versions"))!;
            Assert.Equal(new[] { 1, 2 }, listedVersions.Select(v => v.VersionNumber));
            var detail = (await client.GetResultAsync<TemplateVersionDetailDto>($"/api/v1/template-versions/{complete.TemplateVersionId}"))!;
            Assert.Equal(complete.TemplateId, detail.TemplateId); Assert.Single(detail.Sections);
            using var updated = await client.PutAsJsonAsync($"/api/v1/templates/{completeId}", new UpdateTemplateDto(name + "-renamed"));
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal(name + "-renamed", (await updated.Content.ReadResultAsync<TemplateDto>())!.Name);
            Assert.Equal(MeetingsPermissions.Keys.Order(), (await new TemplateStore(db).GetPermissionsAsync(default)).Keys.Order());
            await VerifyActasAndNotifications(db, actor.IdUser, type.ActaTypeId, complete);
            // The new route reaches real MySQL, but must not bypass the still-empty acta creation grant.
            var creationGrant = await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == "ACTAS_GESTION");
            if (string.IsNullOrWhiteSpace(creationGrant.PermissionValue))
            {
                using var denied = await ActaApiTests.SendAsync(client, new CreateActaDto(
                    versions[0]!.TemplateVersionId, "No debe persistirse", DateOnly.FromDateTime(DateTime.UtcNow), "R01", [], [], null));
                await ActaApiTests.AssertFailure(denied, HttpStatusCode.Forbidden);
            }
        }
        finally
        {
            if (id > 0 || completeId > 0)
            {
                var ownedTemplates = new[] { id, completeId }.Where(value => value > 0).ToArray();
                await using var cleanup = await db.Database.BeginTransactionAsync();
                await db.Set<TemplateSection>().Where(s => db.Set<TemplateVersion>()
                    .Any(v => ownedTemplates.Contains(v.TemplateId) && v.TemplateVersionId == s.TemplateVersionId)).ExecuteDeleteAsync();
                await db.Set<TemplateVersion>().Where(v => ownedTemplates.Contains(v.TemplateId)).ExecuteDeleteAsync();
                await db.Set<Template>().Where(t => ownedTemplates.Contains(t.TemplateId)).ExecuteDeleteAsync();
                await cleanup.CommitAsync();
                Assert.False(await db.Set<Template>().AnyAsync(t => t.TemplateId == id));
            }
        }
    }

    private static async Task VerifyActasAndNotifications(MeetingsDbContext db, int userId, int typeId, CreatedTemplateWithVersionDto template)
    {
        var fixture = Guid.NewGuid().ToString("N");
        var email = fixture + "@example.invalid";
        var center = await db.Database.SqlQuery<string>($"SELECT c.solution_center_code AS Value FROM sigocalco_authentication_solution_center c JOIN sigocalco_authentication_userrole ur ON ur.solution_center_id=c.solution_center_id WHERE ur.IdUser={userId} LIMIT 1").SingleAsync();
        var actor = new MeetingsIdentity(userId, [], [center], "Organizador de prueba", "organizer-" + email);
        var actaIds = new List<int>();
        var calendarCount = await db.Set<MeetingCalendar>().CountAsync();
        try
        {
            foreach (var mode in new string?[] { null, "PRESENCIAL", "VIRTUAL" })
            {
                var now = DateTime.UtcNow;
                var acta = new Acta { ActaCode = "ACT-" + Guid.NewGuid().ToString("N"), ActaTypeId = typeId,
                    TemplateVersionId = template.TemplateVersionId, Title = "Verificación SMTP " + fixture, DocumentDate = DateOnly.FromDateTime(now),
                    SolutionCenterCode = center, CreatedByUser = userId, CreatedAt = now, UpdatedAt = now };
                var meeting = mode is null ? null : new ScheduledMeeting { Modality = mode, StartAtUtc = now.AddDays(1), EndAtUtc = now.AddDays(1).AddHours(1),
                    TimeZone = "America/Bogota", LocationDescription = mode == "PRESENCIAL" ? "Sala de prueba" : null, OrganizerUserId = userId, CreatedAt = now, UpdatedAt = now };
                var people = new[] { new Participant { Email = email, UserName = "Participante de prueba", ParticipantType = "EXTERNO" } };
                var drafts = new MeetingNotificationComposer().Compose(acta, meeting, people, actor);
                var request = new NewActa(acta, [new() { TemplateVersionId = template.TemplateVersionId, TemplateSectionId = template.Sections[0].TemplateSectionId,
                    ContentJson = "{\"fecha\":\"2026-11-18\",\"hora\":\"10:00\",\"lugar\":\"Prueba\"}" }], people, meeting, mode == "PRESENCIAL" ? "TEST-ROOM" : null, drafts);
                var result = await new ActaStore(db).CreateAsync(request, default);
                actaIds.Add(result.ActaId);
                Assert.Equal(userId, acta.CreatedByUser);
                if (meeting is not null)
                {
                    if (mode == "PRESENCIAL") Assert.Equal("SIMULADO", (await new MeetingIntegrationService(db,
                        new SimulatedRoomReservationProvider(), NullLogger<MeetingIntegrationService>.Instance).SynchronizeAsync(meeting.MeetingId, default)).State);
                    var notifications = await new MeetingNotificationService(db, new SimulatedNotificationSender()).SendForMeetingAsync(meeting.MeetingId, default);
                    Assert.Equal("SIMULADO", notifications.State); Assert.Single(notifications.Items); Assert.False(notifications.SmtpAccepted);
                }
                else Assert.Null(result.MeetingId);
                var duplicate = await Assert.ThrowsAsync<Meetings.Domain.Exceptions.MeetingsRequestException>(() => new ActaStore(db).CreateAsync(request, default));
                Assert.Equal(409, duplicate.StatusCode);
            }
            Assert.Equal(1, await db.Set<Participant>().CountAsync(p => p.Email == email));
            Assert.Equal(calendarCount, await db.Set<MeetingCalendar>().CountAsync());
            // A late unique-constraint error in notifications rolls back the entire new acta transaction on real MySQL.
            var failedCode = "ACT-" + Guid.NewGuid().ToString("N");
            var failed = new Acta { ActaCode = failedCode, ActaTypeId = typeId, TemplateVersionId = template.TemplateVersionId,
                Title = "Rollback " + fixture, DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow), SolutionCenterCode = center, CreatedByUser = userId,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var failedMeeting = new ScheduledMeeting { Modality = "VIRTUAL", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1),
                OrganizerUserId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var duplicateMessages = new[] { "one-", "two-" }.Select(prefix => new MeetingNotification
            {
                RecipientEmail = prefix + email, RecipientName = "Prueba", MessageId = fixture + "@sigocalco.local", Subject = "Rollback",
                TextBody = "Prueba", HtmlBody = "<p>Prueba</p>", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }).ToArray();
            await Assert.ThrowsAsync<Meetings.Domain.Exceptions.MeetingsRequestException>(() => new ActaStore(db).CreateAsync(
                new(failed, [], [], failedMeeting, null, duplicateMessages), default));
            Assert.False(await db.Set<Acta>().AnyAsync(a => a.ActaCode == failedCode));
        }
        finally
        {
            db.ChangeTracker.Clear();
            await using var cleanup = await db.Database.BeginTransactionAsync();
            var meetings = await db.Set<ScheduledMeeting>().Where(m => actaIds.Contains(m.ActaId)).Select(m => m.MeetingId).ToArrayAsync();
            await db.Set<MeetingNotification>().Where(n => actaIds.Contains(n.ActaId)).ExecuteDeleteAsync();
            await db.Set<MeetingReservation>().Where(r => meetings.Contains(r.MeetingId)).ExecuteDeleteAsync();
            await db.Set<ScheduledMeeting>().Where(m => meetings.Contains(m.MeetingId)).ExecuteDeleteAsync();
            await db.Set<ActaParticipant>().Where(p => actaIds.Contains(p.ActaId)).ExecuteDeleteAsync();
            await db.Set<ActaSection>().Where(s => actaIds.Contains(s.ActaId)).ExecuteDeleteAsync();
            await db.Set<Acta>().Where(a => actaIds.Contains(a.ActaId)).ExecuteDeleteAsync();
            await db.Set<Participant>().Where(p => p.Email == email).ExecuteDeleteAsync();
            await cleanup.CommitAsync();
        }
    }

    private static async Task VerifyActualSchema(MeetingsDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<SchemaColumn>("""
            SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, COLUMN_TYPE AS DataType, IS_NULLABLE AS IsNullable
            FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_'
            """).ToListAsync();
        var model = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(18, model.Length);
        Assert.Equal(18, columns.Select(c => c.TableName).Distinct().Count());
        Assert.Equal(model.Sum(e => e.GetProperties().Count()), columns.Count);
        foreach (var entity in model)
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            foreach (var property in entity.GetProperties())
            {
                var actual = Assert.Single(columns, c => c.TableName == table.Name && c.ColumnName == property.GetColumnName(table));
                Assert.Equal(property.GetColumnType(), actual.DataType);
                Assert.Equal(property.IsNullable ? "YES" : "NO", actual.IsNullable);
            }
        }
        var foreignKeys = await db.Database.SqlQueryRaw<string>("""
            SELECT CONSTRAINT_NAME AS Value FROM information_schema.REFERENTIAL_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_'
            """).ToListAsync();
        Assert.Equal(model.SelectMany(e => e.GetForeignKeys()).Select(fk => fk.GetConstraintName()).OrderBy(n => n),
            foreignKeys.OrderBy(n => n));
        var indexes = await db.Database.SqlQueryRaw<string>("""
            SELECT DISTINCT CONCAT(TABLE_NAME,'.',INDEX_NAME) AS Value FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_'
            """).ToListAsync();
        foreach (var entity in model)
            foreach (var index in entity.GetIndexes())
                Assert.Contains(entity.GetTableName() + "." + index.GetDatabaseName(), indexes);
    }

    public sealed class VerificationActor
    {
        public int IdUser { get; set; }
        public int IdRole { get; set; }
        public string NameRole { get; set; } = "";
    }
    public sealed class SchemaColumn
    {
        public string TableName { get; set; } = "";
        public string ColumnName { get; set; } = "";
        public string DataType { get; set; } = "";
        public string IsNullable { get; set; } = "";
    }
}
