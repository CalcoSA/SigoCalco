using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meetings.Test;

public sealed class TemplateApiTests
{
    [Fact]
    public async Task Listing_has_constant_query_count_and_no_N_plus_one()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        for (var i = 0; i < 4; i++)
        {
            var id = await Create(client);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", Version())).StatusCode);
        }
        factory.Commands.Reset();
        var page = await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates?pageSize=2");
        Assert.Equal(4, page!.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(6, factory.Commands.Count); // permissions + types + count + templates + versions + sections
    }

    public static JsonElement Form => JsonSerializer.Deserialize<JsonElement>("""
        {"fields":[{"key":"fecha","label":"Fecha","type":"date","required":true},{"key":"hora","label":"Hora","type":"time","required":true},{"key":"lugar","label":"Lugar","type":"text","required":true}]}
        """);
    private static CreateTemplateVersionDto Version(int order = 1) => new([
        new("Información general", "FORMULARIO", order, true, Form)]);
    private static async Task<int> Create(HttpClient client, int type = 1, string scope = "GLOBAL", string? center = null)
    {
        var response = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(type, "Acta general", scope, center));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadResultAsync<CreatedTemplateDto>())!.TemplateId;
    }

    [Theory]
    [InlineData("/api/v1/acta-types", "GET")]
    [InlineData("/api/v1/templates", "GET")]
    [InlineData("/api/v1/templates", "POST")]
    [InlineData("/api/v1/templates/1/versions", "POST")]
    public async Task Endpoints_require_authentication(string path, string method)
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client(false);
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Four_endpoints_create_nested_versions_and_paginate_templates()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var types = await client.GetResultAsync<ActaTypeDto[]>("/api/v1/acta-types");
        Assert.Equal(3, types!.Length);
        var id = await Create(client);
        for (var number = 1; number <= 2; number++)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", Version());
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var version = await response.Content.ReadResultAsync<TemplateVersionDto>();
            Assert.Equal(number, version!.VersionNumber);
            Assert.Equal(1, version.CreatedByUser);
            Assert.Single(version.Sections);
            Assert.Equal(Form.GetRawText(), version.Sections[0].ConfigurationJson!.Value.GetRawText());
        }
        await Create(client, 3, "CENTRO", "R01");
        var all = await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates?page=1&pageSize=1");
        Assert.Equal(2, all!.TotalCount);
        Assert.Equal(2, all.TotalPages);
        Assert.Single(all.Items);
        Assert.Equal(new[] { 1, 2 }, all.Items[0].Versions.Select(v => v.VersionNumber));
        var filtered = await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates?actaTypeId=3&scope=CENTRO");
        Assert.Single(filtered!.Items);
        Assert.Equal(3, filtered.Items[0].ActaTypeId);
        var page2 = await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates?page=2&pageSize=1");
        Assert.NotEqual(id, page2!.Items[0].TemplateId);
    }

    [Theory]
    [InlineData(2, "CENTRO", "R01", HttpStatusCode.BadRequest)]
    [InlineData(1, "GLOBAL", "R01", HttpStatusCode.BadRequest)]
    [InlineData(1, "CENTRO", null, HttpStatusCode.BadRequest)]
    [InlineData(1, "CENTRO", "R02", HttpStatusCode.Forbidden)]
    [InlineData(1, "CENTRO", "INEXISTENTE", HttpStatusCode.Forbidden)]
    [InlineData(99, "GLOBAL", null, HttpStatusCode.BadRequest)]
    [InlineData(1, "INVALID", null, HttpStatusCode.BadRequest)]
    public async Task Validates_scope_type_and_trusted_center(int type, string scope, string? center, HttpStatusCode status)
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var result = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(type, "Prueba", scope, center));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task Missing_permissions_deny_admin_and_cannot_be_supplied_in_body()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            db.RemoveRange(db.Set<MeetingsPermission>());
            await db.SaveChangesAsync();
        }
        var denied = await client.PostAsJsonAsync("/api/v1/templates", new { actaTypeId = 1, name = "X", scope = "GLOBAL", roles = new[] { "Administrador" }, createdByUser = 999 });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/templates")).StatusCode);
    }

    [Fact]
    public async Task Visibility_and_operation_permissions_are_independent()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var id = await Create(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            db.Add(new Template { ActaTypeId = 1, Name = "Other center", Scope = "CENTRO", SolutionCenterCode = "R02", CreatedByUser = 9, CreatedAt = DateTime.UtcNow });
            db.Remove(await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == "ACTA_REUNION_VERSION_CREACION"));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", Version())).StatusCode);
        Assert.Single((await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!.Items);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            db.Add(new MeetingsPermission { PermissionKey = "VIEW_ALL_SOLUTION_CENTERS", PermissionValue = " ADMINISTRADOR, COSTOS " });
            await db.SaveChangesAsync();
        }
        Assert.Equal(2, (await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!.Items.Count);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1, "X", "CENTRO", "R02"))).StatusCode);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?page=2147483647&pageSize=100")]
    [InlineData("?scope=invalid")]
    [InlineData("?actaTypeId=0")]
    public async Task Rejects_invalid_query(string query)
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/templates" + query)).StatusCode);
    }

    [Fact]
    public async Task Invalid_version_is_not_persisted_and_creator_is_trusted()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var response = await client.PostAsJsonAsync("/api/v1/templates",
            new { actaTypeId = 1, name = "X", scope = "GLOBAL", createdByUser = 999, roles = new[] { "COSTOS" } });
        var id = (await response.Content.ReadResultAsync<CreatedTemplateDto>())!.TemplateId;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", Version(2))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", new CreateTemplateVersionDto([]))).StatusCode);
        var malformed = new StringContent("""{"sections":[{"title":"X","contentType":"FORMULARIO","sortOrder":1,"configurationJson":{"fields": }}]}""", Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/v1/templates/{id}/versions", malformed)).StatusCode);
        var item = (await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!.Items.Single();
        Assert.Equal(1, item.CreatedByUser);
        Assert.Empty(item.Versions);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/templates/999/versions", Version())).StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("identity")]
    [InlineData("role")]
    public async Task Rejects_invalid_tokens_or_identity(string fault)
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        var token = fault switch
        {
            "issuer" => MeetingsApiFactory.Token(issuer: "other"),
            "audience" => MeetingsApiFactory.Token(audience: "other"),
            "expired" => MeetingsApiFactory.Token(expired: true),
            "signature" => MeetingsApiFactory.Token(secret: MeetingsApiFactory.Secret + "wrong"),
            "identity" => MeetingsApiFactory.Token(idUser: "999"),
            _ => MeetingsApiFactory.Token(role: "COSTOS")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(fault == "role" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/v1/acta-types")).StatusCode);
    }

    [Fact]
    public async Task Legacy_routes_are_not_exposed()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Minutes/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/Meetings", new { })).StatusCode);
    }
}

