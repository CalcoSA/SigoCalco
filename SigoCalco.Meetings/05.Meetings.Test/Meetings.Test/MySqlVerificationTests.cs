using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
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
    // The authorized scope stays exactly one template, two versions and two sections.
    // Exercise the four HTTP endpoints with those same records, then remove only their IDs.
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
        var token = issuer.GenerateToken(new UserDto
        {
            IdUser = actor.IdUser, IdRole = actor.IdRole, NameRole = actor.NameRole, UserLogin = "automated-verification"
        }, Guid.NewGuid().ToString("D"));
        using var factory = new MySqlApiFactory(settings);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        var types = await client.GetResultAsync<ActaTypeDto[]>("/api/v1/acta-types");
        Assert.Equal(3, types!.Length);
        var type = await db.Set<ActaType>().SingleAsync(t => t.Code == "REUNION");
        var name = "CODEX-MIGRATION-VERIFY-" + Guid.NewGuid().ToString("N");
        var id = 0;
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
            // The new route reaches real MySQL, but must not bypass the still-empty acta creation grant.
            var creationGrant = await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == "ACTA_REUNION_CREACION");
            if (string.IsNullOrWhiteSpace(creationGrant.PermissionValue))
            {
                using var denied = await ActaApiTests.SendAsync(client, new CreateActaDto(
                    versions[0]!.TemplateVersionId, "No debe persistirse", DateOnly.FromDateTime(DateTime.UtcNow), "R01", [], [], null));
                await ActaApiTests.AssertFailure(denied, HttpStatusCode.Forbidden);
            }
        }
        finally
        {
            if (id > 0)
            {
                await using var cleanup = await db.Database.BeginTransactionAsync();
                await db.Set<TemplateSection>().Where(s => db.Set<TemplateVersion>()
                    .Any(v => v.TemplateId == id && v.TemplateVersionId == s.TemplateVersionId)).ExecuteDeleteAsync();
                await db.Set<TemplateVersion>().Where(v => v.TemplateId == id).ExecuteDeleteAsync();
                await db.Set<Template>().Where(t => t.TemplateId == id && t.Name == name).ExecuteDeleteAsync();
                await cleanup.CommitAsync();
                Assert.False(await db.Set<Template>().AnyAsync(t => t.TemplateId == id));
            }
        }
    }

    private static async Task VerifyActualSchema(MeetingsDbContext db)
    {
        var columns = await db.Database.SqlQueryRaw<SchemaColumn>("""
            SELECT TABLE_NAME AS TableName, COLUMN_NAME AS ColumnName, COLUMN_TYPE AS DataType, IS_NULLABLE AS IsNullable
            FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='bdcalco_sigocalco' AND LEFT(TABLE_NAME,19)='sigocalco_meetings_'
            """).ToListAsync();
        var model = db.Model.GetEntityTypes().ToArray();
        Assert.Equal(17, model.Length);
        Assert.Equal(17, columns.Select(c => c.TableName).Distinct().Count());
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
