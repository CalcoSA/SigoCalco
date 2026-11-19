using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Meetings.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;

namespace Meetings.Test;

public sealed class IsolatedPermissionMySqlFactAttribute : FactAttribute
{
    public IsolatedPermissionMySqlFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("MEETINGS_APPROVED_PERMISSION_MYSQL") != "1")
            Skip = "Requiere MySQL local y autorización para crear/eliminar una base efímera de permisos, sin escribir en la base real.";
    }
}

public sealed class PermissionMySqlTests
{
    private static MeetingsDbContext Context(string connection) => new(new DbContextOptionsBuilder<MeetingsDbContext>()
        .UseMySql(connection, new MySqlServerVersion(new Version(8, 0, 46))).Options);

    [IsolatedPermissionMySqlFact]
    public async Task Isolated_mysql_http_concurrency_and_persistence_leave_real_permissions_untouched()
    {
        var path = Environment.GetEnvironmentVariable("MEETINGS_MYSQL_LOCAL_SETTINGS") ?? throw new InvalidOperationException("Indique el archivo local sin exponer credenciales.");
        using var local = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var realConnection = local.RootElement.GetProperty("ConnectionStrings").GetProperty("MeetingsDatabase").GetString()!;
        var options = new MySqlConnectionStringBuilder(realConnection);
        Assert.Equal("127.0.0.1", options.Server); Assert.Equal(3306u, options.Port); Assert.Equal("bdcalco_sigocalco", options.Database);
        await using var real = Context(realConnection);
        Assert.Equal("6f5f45b3-9fc3-11f1-b008-b082e2815ab3", await real.Database.SqlQueryRaw<string>("SELECT @@server_uuid AS Value").SingleAsync());
        var before = JsonSerializer.Serialize(await new PermissionStore(real).GetAsync(default));
        var realDirectory = new AuthenticationDirectory(real);
        var catalog = await realDirectory.GetActiveRoleNamesAsync(default);
        Assert.Contains("Administrador", catalog);
        var actor = await real.Database.SqlQueryRaw<MySqlVerificationTests.VerificationActor>("""
            SELECT ur.IdUser, r.IdRole, r.nameRole AS NameRole
            FROM sigocalco_authentication_userrole ur
            JOIN sigocalco_authentication_users u ON u.IdUser=ur.IdUser AND u.statusUser=1
            JOIN sigocalco_authentication_role r ON r.IdRole=ur.IdRole AND r.statusRole=1
            WHERE UPPER(TRIM(r.nameRole))='ADMINISTRADOR' ORDER BY ur.IdUser LIMIT 1
            """).SingleAsync();
        Assert.True(PermissionAdministration.IsAdministrator(await realDirectory.ResolveAsync(actor.IdUser, actor.IdRole, actor.NameRole, default)));

        var isolatedName = "sigocalco_permissions_test_" + Guid.NewGuid().ToString("N");
        Assert.Matches("^sigocalco_permissions_test_[a-f0-9]{32}$", isolatedName);
        options.Database = isolatedName; options.Pooling = false;
        var isolatedConnection = options.ConnectionString;
        var created = false;
        try
        {
            // Identifiers are generated here, validated above, and never derived from configuration/user input.
#pragma warning disable EF1002 // MySQL identifiers cannot be parameters; the GUID-only database name is validated above.
            await real.Database.ExecuteSqlRawAsync($"CREATE DATABASE `{isolatedName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
#pragma warning restore EF1002
            created = true;
            await using (var isolated = Context(isolatedConnection))
            {
                Assert.Equal(isolatedName, await isolated.Database.SqlQueryRaw<string>("SELECT DATABASE() AS Value").SingleAsync());
#pragma warning disable EF1002 // Only the validated ephemeral database identifier is interpolated; the source is read-only.
                await isolated.Database.ExecuteSqlRawAsync($"CREATE TABLE `{isolatedName}`.sigocalco_meetings_permission LIKE bdcalco_sigocalco.sigocalco_meetings_permission");
#pragma warning restore EF1002
                isolated.AddRange(MeetingsPermissions.Keys.Select(k => new MeetingsPermission { PermissionKey = k, PermissionValue = "" }));
                await isolated.SaveChangesAsync();
            }
            using var factory = new PermissionMySqlFactory(isolatedConnection, realConnection);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
            var jwtConfig = new ConfigurationBuilder().AddInMemoryCollection(PermissionMySqlFactory.JwtSettings).Build();
            var issued = new JwtTokenApplication(jwtConfig).GenerateToken(new UserDto
            {
                IdUser = actor.IdUser, IdRole = actor.IdRole, NameRole = actor.NameRole, UserLogin = "isolated-permission-test"
            }, Guid.NewGuid().ToString("D"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
            Assert.Equal(6, (await client.GetResultAsync<PermissionDto[]>("/api/v1/permissions"))!.Length);
            using (var added = await PermissionApiTests.Send(client, "POST", MeetingsPermissions.GlobalRead))
            {
                Assert.Equal(HttpStatusCode.OK, added.StatusCode);
                Assert.Equal(["Administrador"], (await added.Content.ReadResultAsync<PermissionDto>())!.Roles);
            }
            // A fresh context sees persisted grants immediately, using the same functional authorization helper as templates/actas.
            await using (var fresh = Context(isolatedConnection))
                Assert.True(MeetingsPermissions.Allowed(await new TemplateStore(fresh).GetPermissionsAsync(default),
                    new(actor.IdUser, ["Administrador"], []), MeetingsPermissions.GlobalRead));
            using (var duplicate = await PermissionApiTests.Send(client, "POST", MeetingsPermissions.GlobalRead))
                await ActaApiTests.AssertFailure(duplicate, HttpStatusCode.Conflict);
            using (var invalid = await client.PutAsJsonAsync("/api/v1/permissions/PLANTILLAS_GLOBALES_LECTURA", new { roles = new[] { "Administrador", "ROLE-NOT-IN-AUTHENTICATION" } }))
                await ActaApiTests.AssertFailure(invalid, HttpStatusCode.BadRequest);
            using (var cleared = await client.PutAsJsonAsync("/api/v1/permissions/PLANTILLAS_GLOBALES_LECTURA", new { roles = Array.Empty<string>() }))
            {
                Assert.Equal(HttpStatusCode.OK, cleared.StatusCode); Assert.Empty((await cleared.Content.ReadResultAsync<PermissionDto>())!.Roles);
            }
            await using (var fresh = Context(isolatedConnection))
                Assert.False(MeetingsPermissions.Allowed(await new TemplateStore(fresh).GetPermissionsAsync(default),
                    new(actor.IdUser, ["Administrador"], []), MeetingsPermissions.GlobalRead));
            var responses = await Task.WhenAll(PermissionApiTests.Send(client, "POST"), PermissionApiTests.Send(client, "POST"));
            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, responses.Select(r => r.StatusCode).Order());
            foreach (var response in responses) response.Dispose();

            // Store-only fixtures exercise distinct concurrent values; no fabricated roles are submitted to the real catalog/API.
            await using (var setup = Context(isolatedConnection))
                await new PermissionStore(setup).ReplaceRolesAsync(MeetingsPermissions.CenterRead, ["Preserved fixture"], default);
            await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
            {
                await using var db = Context(isolatedConnection);
                await new PermissionStore(db).AddRoleAsync(MeetingsPermissions.CenterRead, $"Concurrent fixture {i}", default);
            }));
            await using (var verify = Context(isolatedConnection))
            {
                var saved = (await new PermissionStore(verify).GetAsync(default)).Single(p => p.PermissionKey == MeetingsPermissions.CenterRead);
                Assert.Equal(13, PermissionAdministration.ParseRoles(saved.PermissionValue).Length);
                Assert.Contains("Preserved fixture", PermissionAdministration.ParseRoles(saved.PermissionValue));
                for (var i = 0; i < 12; i++) Assert.Contains($"Concurrent fixture {i}", PermissionAdministration.ParseRoles(saved.PermissionValue));
            }
            // PUT waits for an existing row lock; no partial list is ever committed.
            await using (var blocker = Context(isolatedConnection))
            {
                await using var transaction = await blocker.Database.BeginTransactionAsync();
                await blocker.Set<MeetingsPermission>().FromSqlRaw("SELECT * FROM sigocalco_meetings_permission WHERE permission_key='ACTAS_GESTION' FOR UPDATE").AsNoTracking().ToListAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var write = Task.Run(async () =>
                {
                    await using var writer = Context(isolatedConnection);
                    return await new PermissionStore(writer).ReplaceRolesAsync(MeetingsPermissions.ActasManage, ["Whole fixture A", "Whole fixture B"], timeout.Token);
                });
                await Task.Delay(150);
                Assert.False(write.IsCompleted);
                await transaction.CommitAsync();
                Assert.Equal("Whole fixture A,Whole fixture B", (await write).PermissionValue);
            }
            await using (var fresh = Context(isolatedConnection))
            {
                Assert.Equal(6, await fresh.Set<MeetingsPermission>().CountAsync());
                Assert.Equal(MeetingsPermissions.Keys.Order(), (await new PermissionStore(fresh).GetAsync(default)).Select(p => p.PermissionKey).Order());
            }
        }
        finally
        {
            if (created)
            {
                if (!Regex.IsMatch(isolatedName, "^sigocalco_permissions_test_[a-f0-9]{32}$") || isolatedName == "bdcalco_sigocalco")
                    throw new InvalidOperationException("No se pudo verificar el nombre de la base temporal; no eliminar.");
#pragma warning disable EF1002 // Cleanup is restricted to the validated database created by this test.
                await real.Database.ExecuteSqlRawAsync($"DROP DATABASE `{isolatedName}`");
#pragma warning restore EF1002
            }
            Assert.Equal(before, JsonSerializer.Serialize(await new PermissionStore(real).GetAsync(default)));
        }
    }

    private sealed class AuthenticationReadContext(string connection) : IDisposable
    {
        public MeetingsDbContext Db { get; } = Context(connection);
        public void Dispose() => Db.Dispose();
    }

    private sealed class PermissionMySqlFactory(string isolatedConnection, string realConnection) : WebApplicationFactory<Program>
    {
        public static readonly Dictionary<string, string?> JwtSettings = new()
        {
            ["Jwt:Secret"] = MeetingsApiFactory.Secret, ["Jwt:Issuer"] = "permission-isolated-tests", ["Jwt:Audience"] = "permission-isolated-tests"
        };
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(JwtSettings).AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MeetingsDatabase"] = isolatedConnection, ["Smtp:Enabled"] = "false"
            }));
            builder.ConfigureServices(services =>
            {
                services.AddScoped(_ => new AuthenticationReadContext(realConnection));
                services.RemoveAll<IAuthenticationDirectory>();
                services.AddScoped<IAuthenticationDirectory>(p => new AuthenticationDirectory(p.GetRequiredService<AuthenticationReadContext>().Db));
            });
        }
    }
}
