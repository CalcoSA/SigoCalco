using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class PermissionApiTests
{
    private const string Root = "/api/v1/permissions";
    private const string Key = MeetingsPermissions.ActasRead;

    [Fact]
    public void Swagger_documents_all_three_admin_operations_and_their_contracts()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var document = factory.Services.GetRequiredService<Swashbuckle.AspNetCore.Swagger.ISwaggerProvider>().GetSwagger("v1");
        foreach (var (path, method, hasBody) in new[]
        {
            (Root, "get", false), (Root + "/{permissionKey}/roles", "post", true), (Root + "/{permissionKey}", "put", true)
        })
        {
            var operation = document.Paths[path].Operations!.Single(o => o.Key.ToString().Equals(method, StringComparison.OrdinalIgnoreCase)).Value;
            Assert.Contains("Solo Administrador", operation.Description);
            Assert.NotNull(operation.Summary);
            Assert.True(operation.Responses!.ContainsKey("200"));
            Assert.True(operation.Responses.ContainsKey("401"));
            Assert.True(operation.Responses.ContainsKey("403"));
            if (hasBody)
            {
                Assert.NotNull(operation.RequestBody);
                Assert.True(operation.Responses.ContainsKey("400"));
                Assert.True(operation.Responses.ContainsKey("404"));
                Assert.True(operation.Responses.ContainsKey("409"));
            }
        }
    }

    [Fact]
    public async Task Lists_exactly_six_permissions_with_roles_and_empty_arrays()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var permissions = (await client.GetResultAsync<PermissionDto[]>(Root))!;
        Assert.Equal(MeetingsPermissions.Keys.Order(), permissions.Select(p => p.PermissionKey).Order());
        Assert.All(permissions, p => Assert.True(p.PermissionId > 0));
        Assert.Equal(["ADMINISTRADOR"], permissions.Single(p => p.PermissionKey == MeetingsPermissions.GlobalManage).Roles);
        Assert.Empty(permissions.Single(p => p.PermissionKey == Key).Roles);
    }

    [Fact]
    public async Task Null_database_value_is_returned_as_empty_array_without_changing_the_schema()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE sigocalco_meetings_permission (permission_id INTEGER PRIMARY KEY, permission_key TEXT, permission_value TEXT NULL); INSERT INTO sigocalco_meetings_permission VALUES (1, 'ACTAS_LECTURA', NULL);";
            await command.ExecuteNonQueryAsync();
        }
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<MeetingsDbContext>();
            services.AddScoped<MeetingsDbContext>(_ => new TestMeetingsContext(
                new DbContextOptionsBuilder<MeetingsDbContext>().UseSqlite(connection).Options));
        }};
        using var client = factory.Client();
        Assert.Empty(Assert.Single((await client.GetResultAsync<PermissionDto[]>(Root))!).Roles);
    }

    [Fact]
    public async Task Add_preserves_previous_roles_uses_catalog_spelling_and_rejects_duplicate()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var added = await client.PostAsJsonAsync($"{Root}/{MeetingsPermissions.GlobalManage}/roles", new { roleName = " lector DE prueba " });
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        var permission = (await added.Content.ReadResultAsync<PermissionDto>())!;
        Assert.Equal(["ADMINISTRADOR", "Lector de prueba"], permission.Roles);
        using var duplicate = await client.PostAsJsonAsync($"{Root}/{MeetingsPermissions.GlobalManage}/roles", new { roleName = "LECTOR DE PRUEBA" });
        await ActaApiTests.AssertFailure(duplicate, HttpStatusCode.Conflict);
        var saved = (await client.GetResultAsync<PermissionDto[]>(Root))!.Single(p => p.PermissionId == permission.PermissionId);
        Assert.Equal(permission.Roles, saved.Roles);
    }

    [Fact]
    public async Task Replace_multiple_roles_and_empty_list_are_atomic_and_keep_key_and_id()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var original = (await client.GetResultAsync<PermissionDto[]>(Root))!.Single(p => p.PermissionKey == Key);
        using var update = await client.PutAsJsonAsync($"{Root}/{Key}", new { roles = new[] { "administrador", "Operador de prueba" } });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var saved = (await update.Content.ReadResultAsync<PermissionDto>())!;
        Assert.Equal(original.PermissionId, saved.PermissionId); Assert.Equal(Key, saved.PermissionKey);
        Assert.Equal(["Administrador", "Operador de prueba"], saved.Roles);
        using var invalid = await client.PutAsJsonAsync($"{Root}/{Key}", new { roles = new[] { "Lector de prueba", "No existe" } });
        await ActaApiTests.AssertFailure(invalid, HttpStatusCode.BadRequest);
        Assert.Equal(saved.Roles, (await client.GetResultAsync<PermissionDto[]>(Root))!.Single(p => p.PermissionKey == Key).Roles);
        using var empty = await client.PutAsJsonAsync($"{Root}/{Key}", new { roles = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode); Assert.Empty((await empty.Content.ReadResultAsync<PermissionDto>())!.Roles);
        Assert.Equal(6, (await client.GetResultAsync<PermissionDto[]>(Root))!.Length);
    }

    [Theory]
    [InlineData("POST", "{\"roleName\":\"No existe\"}")]
    [InlineData("POST", "{\"roleName\":\"\"}")]
    [InlineData("POST", "{\"roleName\":\"Administrador,Otro\"}")]
    [InlineData("POST", "{\"roleName\":\"Administrador，Otro\"}")]
    [InlineData("POST", "{\"roleName\":null}")]
    [InlineData("POST", "{}")]
    [InlineData("PUT", "{\"roles\":[\"Administrador\",\" ADMINISTRADOR \"]}")]
    [InlineData("PUT", "{\"roles\":[null]}")]
    [InlineData("PUT", "{\"roles\":[\" \"]}")]
    [InlineData("PUT", "{\"roles\":null}")]
    [InlineData("PUT", "{}")]
    [InlineData("PUT", "{\"roles\":[],\"permissionKey\":\"Otra\"}")]
    public async Task Invalid_bodies_never_change_assignments(string method, string json)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var request = new HttpRequestMessage(new(method), $"{Root}/{Key}" + (method == "POST" ? "/roles" : ""))
            { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request);
        await ActaApiTests.AssertFailure(response, HttpStatusCode.BadRequest);
        Assert.Empty((await client.GetResultAsync<PermissionDto[]>(Root))!.Single(p => p.PermissionKey == Key).Roles);
    }

    [Theory]
    [InlineData("POST", false)] [InlineData("PUT", false)]
    [InlineData("POST", true)] [InlineData("PUT", true)]
    public async Task Missing_permission_is_not_created(string method, bool knownButDeleted)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        if (knownButDeleted)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MeetingsDbContext>().Set<MeetingsPermission>().Where(p => p.PermissionKey == Key).ExecuteDeleteAsync();
        }
        using var response = await Send(client, method, knownButDeleted ? Key : "PERMISO_NUEVO");
        await ActaApiTests.AssertFailure(response, HttpStatusCode.NotFound);
        Assert.Equal(knownButDeleted ? 5 : 6, (await client.GetResultAsync<PermissionDto[]>(Root))!.Length);
    }

    [Theory]
    [InlineData("GET", false)] [InlineData("POST", false)] [InlineData("PUT", false)]
    [InlineData("GET", true)] [InlineData("POST", true)] [InlineData("PUT", true)]
    public async Task Requires_valid_jwt(string method, bool expired)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(false);
        if (expired) client.DefaultRequestHeaders.Authorization = new("Bearer", MeetingsApiFactory.Token(expired: true));
        using var response = await Send(client, method);
        await ActaApiTests.AssertFailure(response, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", false)] [InlineData("POST", false)] [InlineData("PUT", false)]
    [InlineData("GET", true)] [InlineData("POST", true)] [InlineData("PUT", true)]
    public async Task Real_nonadministrator_is_denied_even_if_the_token_claims_admin(string method, bool adminClaim)
    {
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<IAuthenticationDirectory>(); services.AddScoped<IAuthenticationDirectory, NonAdministratorDirectory>();
        }};
        using var client = factory.Client(false);
        client.DefaultRequestHeaders.Authorization = new("Bearer", MeetingsApiFactory.Token(role: adminClaim ? "Administrador" : "Lector de prueba"));
        using var response = await Send(client, method);
        await ActaApiTests.AssertFailure(response, HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("GET")] [InlineData("POST")] [InlineData("PUT")]
    public async Task Revoked_database_role_is_denied_despite_previously_valid_admin_token(string method)
    {
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<IAuthenticationDirectory>(); services.AddScoped<IAuthenticationDirectory, RevokedDirectory>();
        }};
        using var client = factory.Client(); using var response = await Send(client, method);
        await ActaApiTests.AssertFailure(response, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Updates_immediately_change_functional_permissions_without_restarting_or_affecting_admin_access()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var acta = await ActaApiTests.PrepareAsync(factory, client, grant: false);
        using (var denied = await ActaApiTests.SendAsync(client, acta)) await ActaApiTests.AssertFailure(denied, HttpStatusCode.Forbidden);
        using (var grant = await Send(client, "POST", MeetingsPermissions.ActasManage)) Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        using (var created = await ActaApiTests.SendAsync(client, acta)) Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using (var revoke = await client.PutAsJsonAsync($"{Root}/{MeetingsPermissions.ActasManage}", new { roles = Array.Empty<string>() })) Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        using (var denied = await ActaApiTests.SendAsync(client, acta)) await ActaApiTests.AssertFailure(denied, HttpStatusCode.Forbidden);
        using (var revoke = await client.PutAsJsonAsync($"{Root}/{MeetingsPermissions.GlobalManage}", new { roles = Array.Empty<string>() })) Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        using (var denied = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1, "Sin acceso", "GLOBAL", null)))
            await ActaApiTests.AssertFailure(denied, HttpStatusCode.Forbidden);
        // Administrative policy does not depend on functional grants; the actual admin can restore the grant.
        using (var grant = await Send(client, "POST", MeetingsPermissions.GlobalManage)) Assert.Equal(HttpStatusCode.OK, grant.StatusCode);
        using (var created = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1, "Permitida", "GLOBAL", null))) Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    [Fact]
    public async Task Overflow_does_not_truncate_or_remove_existing_roles()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var original = new string('x', 2000);
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<MeetingsDbContext>().Set<MeetingsPermission>().Where(p => p.PermissionKey == Key)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PermissionValue, original));
        using var response = await Send(client, "POST");
        await ActaApiTests.AssertFailure(response, HttpStatusCode.BadRequest);
        Assert.Equal([original], (await client.GetResultAsync<PermissionDto[]>(Root))!.Single(p => p.PermissionKey == Key).Roles);
    }

    internal static Task<HttpResponseMessage> Send(HttpClient client, string method, string key = Key) => method switch
    {
        "GET" => client.GetAsync(Root),
        "POST" => client.PostAsJsonAsync($"{Root}/{key}/roles", new { roleName = "Administrador" }),
        _ => client.PutAsJsonAsync($"{Root}/{key}", new { roles = new[] { "Administrador" } })
    };

    private sealed class NonAdministratorDirectory : IAuthenticationDirectory
    {
        public Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct) =>
            Task.FromResult(new MeetingsIdentity(1, ["Lector de prueba"], ["R01"]));
        public Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(CancellationToken ct) => throw new InvalidOperationException("El catálogo no debe consultarse sin autorización.");
    }
    private sealed class RevokedDirectory : IAuthenticationDirectory
    {
        public Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct) => throw MeetingsRequestException.Forbidden();
        public Task<IReadOnlyList<string>> GetActiveRoleNamesAsync(CancellationToken ct) => throw new InvalidOperationException();
    }
}
