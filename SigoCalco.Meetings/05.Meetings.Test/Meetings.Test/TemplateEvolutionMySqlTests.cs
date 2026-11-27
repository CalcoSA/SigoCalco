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

public sealed class TemplateEvolutionMySqlFactAttribute : FactAttribute
{
    public TemplateEvolutionMySqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEETINGS_APPROVED_TEMPLATE_MYSQL") != "1")
            Skip = "Requiere MySQL local y una base efímera propia; nunca migra bdcalco_sigocalco.";
    }
}

public sealed class TemplateEvolutionMySqlTests
{
    private static MeetingsDbContext Context(string connection) => new(new DbContextOptionsBuilder<MeetingsDbContext>()
        .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46))).Options);

    internal static async Task ApplyMigration(MeetingsDbContext db)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", "08_template_text_subtitle_unique_name.sql"));
        // DELIMITER belongs to mysql/Workbench, not the server protocol used by MySqlConnector.
        sql = Regex.Replace(sql, @"^DELIMITER[^\r\n]*", "", RegexOptions.Multiline).Replace("END$$", "END;");
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    [TemplateEvolutionMySqlFact]
    public async Task Migration_guards_history_and_mysql_enforces_concurrent_names_and_new_section_contracts()
    {
        using var local = JsonDocument.Parse(await File.ReadAllTextAsync(Environment.GetEnvironmentVariable("MEETINGS_MYSQL_LOCAL_SETTINGS")!));
        var builder = new MySqlConnectionStringBuilder(local.RootElement.GetProperty("ConnectionStrings").GetProperty("MeetingsDatabase").GetString()!);
        Assert.Equal("127.0.0.1", builder.Server); Assert.Equal(3306u, builder.Port); Assert.Equal("bdcalco_sigocalco", builder.Database);
        await using var real = Context(builder.ConnectionString);
        Assert.Equal("6f5f45b3-9fc3-11f1-b008-b082e2815ab3", await real.Database.SqlQueryRaw<string>("SELECT @@server_uuid AS Value").SingleAsync());
        var name = "sigocalco_templates_test_" + Guid.NewGuid().ToString("N");
        Assert.Matches("^sigocalco_templates_test_[a-f0-9]{32}$", name);
        builder.Database = name; builder.Pooling = false;
        var connection = builder.ConnectionString; var created = false;
        try
        {
#pragma warning disable EF1002 // Locally generated GUID-only identifier, validated above.
            await real.Database.ExecuteSqlRawAsync($"CREATE DATABASE `{name}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
#pragma warning restore EF1002
            created = true;
            await using (var db = Context(connection))
            {
                foreach (var file in new[] { "02_create_model.sql", "04_add_meeting_notifications.sql", "06_mis_actas_incremental.sql" })
                    await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sql", file)));
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO sigocalco_meetings_acta_type VALUES (1,'REUNION','Acta de reunión'),(3,'ADMINISTRATIVA','Acta administrativa');
                    INSERT INTO sigocalco_meetings_template(template_id,acta_type_id,name,scope,created_by_user,created_at)
                    VALUES (1,1,'Anterior','GLOBAL',1,'2026-11-01'),(2,3,' ANTERIOR ','GLOBAL',1,'2026-11-01');
                    INSERT INTO sigocalco_meetings_template_version(template_version_id,template_id,acta_type_id,version_number,created_by_user,created_at)
                    VALUES (1,1,1,1,1,'2026-11-01');
                    INSERT INTO sigocalco_meetings_template_section(template_section_id,template_version_id,title,content_type,configuration_json,sort_order,is_required)
                    VALUES (1,1,'Texto histórico','TEXTO',NULL,1,0);
                    """);
                var duplicate = await Assert.ThrowsAsync<MySqlException>(() => ApplyMigration(db));
                Assert.Equal(1644, duplicate.Number); Assert.Contains("duplicados", duplicate.Message);
                Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLUMN_NAME='name_normalized'").SingleAsync());
                Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sigocalco_meetings_template").SingleAsync());
                // Cleanup only our deliberately duplicated test fixture; no historical real rows are changed.
                await db.Database.ExecuteSqlRawAsync("DROP PROCEDURE meetings_migrate_template_sections_20261125; DELETE FROM sigocalco_meetings_template WHERE template_id=2;");
                await ApplyMigration(db);
                Assert.Equal("Anterior", (await db.Set<Template>().SingleAsync()).Name);
                var historical = await db.Set<TemplateSection>().SingleAsync();
                Assert.Equal(1, historical.TemplateSectionId); Assert.Equal("Texto histórico", historical.Title);
                Assert.Equal("TEXTO", historical.ContentType); Assert.Null(historical.ConfigurationJson);
                var key = await db.Set<Template>().Select(t => EF.Property<string>(t, "NormalizedName")).SingleAsync();
                Assert.Equal("anterior", key);
                Assert.Equal("utf8mb4_bin", await db.Database.SqlQueryRaw<string>("SELECT COLLATION_NAME AS Value FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='sigocalco_meetings_template' AND COLUMN_NAME='name_normalized'").SingleAsync());
                // Retrying an already applied migration is a guarded refusal, not a reconstruction.
                var repeated = await Assert.ThrowsAsync<MySqlException>(() => ApplyMigration(db));
                Assert.Contains("previa, parcial", repeated.Message);
                await db.Database.ExecuteSqlRawAsync("DROP PROCEDURE meetings_migrate_template_sections_20261125;");
                db.AddRange(MeetingsPermissions.Keys.Select(k => new MeetingsPermission { PermissionKey=k, PermissionValue="Administrador" }));
                await db.SaveChangesAsync();
            }

            using var factory = new Factory(connection);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", MeetingsApiFactory.Token());
            await TemplateSectionEvolutionTests.ExerciseSections(factory.Services, client);
            // Concurrent POSTs through BOTH creation endpoints, across scope/type, must persist one complete graph only.
            var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => i % 2 == 0
                ? client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(3, " NOMBRE CONCURRENTE ", "CENTRO", "R01"))
                : client.PostAsJsonAsync("/api/v1/templates/with-version", TemplateSectionEvolutionTests.Request("nombre concurrente"))));
            Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created);
            Assert.Equal(11, replies.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            foreach (var reply in replies) reply.Dispose();
            await using (var db = Context(connection))
            {
                var winner = await db.Set<Template>().Include(t => t.Versions).ThenInclude(v => v.Sections)
                    .SingleAsync(t => EF.Property<string>(t, "NormalizedName") == "nombre concurrente");
                Assert.Equal(winner.Scope == "GLOBAL" ? 1 : 0, winner.Versions.Count);
                if (winner.Scope == "GLOBAL") Assert.Equal(3, winner.Versions[0].Sections.Count);
            }

            // Skip all application prechecks: the database itself must reject concurrent equivalent keys.
            var direct = await Task.WhenAll(new[] { "ÍNDICE DIRECTO", " índice directo " }.Select(async candidate =>
            {
                await using var db = Context(connection);
                db.Add(new Template { Name=candidate, ActaTypeId=1, Scope="GLOBAL", CreatedByUser=1, CreatedAt=DateTime.UtcNow });
                try { await db.SaveChangesAsync(); return true; }
                catch (DbUpdateException ex) when (ex.InnerException is MySqlException { Number:1062 }) { return false; }
            }));
            Assert.Single(direct, succeeded => succeeded);
            await using (var db = Context(connection))
            {
                Assert.Equal(18, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE()").SingleAsync());
                Assert.Equal(6, await db.Set<MeetingsPermission>().CountAsync());
                Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA=DATABASE()").SingleAsync());
                var oldId = await db.Set<Template>().Where(t => t.Name == "Anterior").Select(t => t.TemplateId).SingleAsync();
                using var same = await client.PutAsJsonAsync($"/api/v1/templates/{oldId}", new UpdateTemplateDto(" ANTERIOR "));
                Assert.Equal(HttpStatusCode.OK, same.StatusCode);
                await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/templates/{oldId}", new UpdateTemplateDto(" NOMBRE CONCURRENTE ")), HttpStatusCode.Conflict);
            }
        }
        finally
        {
            if (created)
            {
                if (!Regex.IsMatch(name, "^sigocalco_templates_test_[a-f0-9]{32}$")) throw new InvalidOperationException("Identificador temporal inesperado.");
#pragma warning disable EF1002 // Cleanup restricted to this test's validated temporary database.
                await real.Database.ExecuteSqlRawAsync($"DROP DATABASE `{name}`");
#pragma warning restore EF1002
            }
        }
    }

    private sealed class Factory(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureLogging(l => l.ClearProviders());
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["ConnectionStrings:MeetingsDatabase"]=connection, ["Jwt:Secret"]=MeetingsApiFactory.Secret,
                ["Jwt:Issuer"]="meetings-tests", ["Jwt:Audience"]="meetings-tests", ["Smtp:Enabled"]="false"
            }));
            builder.ConfigureServices(s => { s.RemoveAll<IAuthenticationDirectory>(); s.AddScoped<IAuthenticationDirectory,StubDirectory>(); });
        }
    }
}
