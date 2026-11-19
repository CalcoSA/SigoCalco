using System.Net;
using System.Net.Http.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meetings.Test;

public sealed class TemplateManagementTests
{
    internal static async Task SetPermissions(MeetingsApiFactory factory, params string[] grants)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        foreach (var permission in await db.Set<MeetingsPermission>().ToArrayAsync())
            permission.PermissionValue = grants.Contains(permission.PermissionKey) ? "  ADMINISTRADOR  " : "";
        await db.SaveChangesAsync();
    }
    internal static CreateTemplateWithVersionDto Request(string scope = "GLOBAL", string? center = null) => new(1, "Plantilla completa", scope, center,
        [new("Información", "FORMULARIO", 1, true, TemplateApiTests.Form)]);
    private static async Task<Template[]> Seed(MeetingsApiFactory factory)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var templates = new[]
        {
            new Template { ActaTypeId = 1, Name = "Global", Scope = "GLOBAL", CreatedByUser = 1, CreatedAt = DateTime.UtcNow },
            new Template { ActaTypeId = 3, Name = "Propia", Scope = "CENTRO", SolutionCenterCode = "R01", CreatedByUser = 1, CreatedAt = DateTime.UtcNow },
            new Template { ActaTypeId = 1, Name = "Ajena", Scope = "CENTRO", SolutionCenterCode = "R02", CreatedByUser = 2, CreatedAt = DateTime.UtcNow }
        };
        foreach (var template in templates) template.Versions.Add(new TemplateVersion
        {
            ActaTypeId = template.ActaTypeId, VersionNumber = 1, CreatedByUser = template.CreatedByUser, CreatedAt = DateTime.UtcNow,
            Sections = [new TemplateSection { Title = "Histórica", ContentType = "TEXTO", SortOrder = 1 }]
        });
        db.AddRange(templates); await db.SaveChangesAsync(); return templates;
    }
    [Theory]
    [InlineData(MeetingsPermissions.GlobalManage, "Global,Propia,Ajena")]
    [InlineData(MeetingsPermissions.GlobalRead, "Global")]
    [InlineData(MeetingsPermissions.CenterManage, "Propia")]
    [InlineData(MeetingsPermissions.CenterRead, "Propia")]
    [InlineData(MeetingsPermissions.GlobalRead + "," + MeetingsPermissions.CenterRead, "Global,Propia")]
    [InlineData(MeetingsPermissions.GlobalManage + "," + MeetingsPermissions.CenterManage, "Global,Propia,Ajena")]
    public async Task Visibility_filters_pagination_and_detail_obey_same_rules(string grants, string expected)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var templates = await Seed(factory); await SetPermissions(factory, grants.Split(','));
        var names = expected.Split(',');
        var listing = (await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates?pageSize=1"))!;
        Assert.Equal(names.Length, listing.TotalCount); Assert.Equal(names.Length, listing.TotalPages); Assert.Single(listing.Items);
        var full = (await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!;
        Assert.Equal(names.Order(), full.Items.Select(t => t.Name).Order());
        Assert.Equal(full.Items.Count, full.Items.Select(t => t.TemplateId).Distinct().Count());
        foreach (var scopeName in new[] { "GLOBAL", "CENTRO" })
        {
            var filtered = (await client.GetResultAsync<PagedTemplatesDto>($"/api/v1/templates?scope={scopeName}&actaTypeId=1"))!;
            var visible = full.Items.Where(t => t.Scope == scopeName && t.ActaTypeId == 1).ToArray();
            Assert.Equal(visible.Length, filtered.TotalCount);
            Assert.Equal(visible.Select(t => t.TemplateId), filtered.Items.Select(t => t.TemplateId));
        }
        foreach (var template in templates)
        {
            var permitted = names.Contains(template.Name);
            using var versions = await client.GetAsync($"/api/v1/templates/{template.TemplateId}/versions");
            using var detail = await client.GetAsync($"/api/v1/template-versions/{template.Versions[0].TemplateVersionId}");
            Assert.Equal(permitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, versions.StatusCode);
            Assert.Equal(permitted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, detail.StatusCode);
            if (permitted)
            {
                var dto = (await detail.Content.ReadResultAsync<TemplateVersionDetailDto>())!;
                Assert.Equal(template.Name, dto.Name); Assert.Equal(template.Scope, dto.Scope);
                Assert.Equal(template.SolutionCenterCode, dto.SolutionCenterCode); Assert.Single(dto.Sections);
            }
        }
    }
    [Theory]
    [InlineData(MeetingsPermissions.GlobalManage, true, false, false)]
    [InlineData(MeetingsPermissions.GlobalRead, false, false, false)]
    [InlineData(MeetingsPermissions.CenterManage, false, true, false)]
    [InlineData(MeetingsPermissions.CenterRead, false, false, false)]
    public async Task Write_permissions_are_independent_from_visibility(string permission, bool global, bool own, bool other)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        var templates = await Seed(factory); await SetPermissions(factory, permission);
        foreach (var pair in templates.Zip(new[] { global, own, other }))
        {
            var expected = pair.Second ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            using var update = await client.PutAsJsonAsync($"/api/v1/templates/{pair.First.TemplateId}", new UpdateTemplateDto("Nombre actualizado"));
            Assert.Equal(expected, update.StatusCode);
            using var version = await client.PostAsJsonAsync($"/api/v1/templates/{pair.First.TemplateId}/versions", new CreateTemplateVersionDto(Request().Sections));
            Assert.Equal(pair.Second ? HttpStatusCode.Created : HttpStatusCode.Forbidden, version.StatusCode);
            using var create = await client.PostAsJsonAsync("/api/v1/templates/with-version", Request(pair.First.Scope, pair.First.SolutionCenterCode));
            Assert.Equal(pair.Second ? HttpStatusCode.Created : HttpStatusCode.Forbidden, create.StatusCode);
        }
    }
    [Fact]
    public async Task Six_dynamic_permissions_deny_empty_or_missing_values_without_restarting()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        await Seed(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            Assert.Equal(MeetingsPermissions.Keys.Order(), (await db.Set<MeetingsPermission>().Select(p => p.PermissionKey).ToArrayAsync()).Order());
        }
        await SetPermissions(factory);
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/templates"), HttpStatusCode.Forbidden);
        await SetPermissions(factory, MeetingsPermissions.GlobalRead);
        Assert.Single((await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!.Items);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            await db.Set<MeetingsPermission>().Where(p => p.PermissionKey == MeetingsPermissions.GlobalRead).ExecuteDeleteAsync();
        }
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/templates"), HttpStatusCode.Forbidden);
    }
    [Theory]
    [InlineData("GLOBAL", null)]
    [InlineData("CENTRO", "R01")]
    public async Task Complete_creation_and_version_queries_preserve_historical_sections(string scopeName, string? center)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var response = await client.PostAsJsonAsync("/api/v1/templates/with-version", Request(scopeName, center));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
        Assert.Equal(1, created.VersionNumber); Assert.Single(created.Sections);
        var before = (await client.GetResultAsync<TemplateVersionDetailDto>($"/api/v1/template-versions/{created.TemplateVersionId}"))!;
        Assert.Equal(1, before.CreatedByUser); Assert.Equal(TemplateApiTests.Form.GetRawText(), before.Sections[0].ConfigurationJson!.Value.GetRawText());
        var section2 = new CreateTemplateSectionDto("Nueva estructura", "FORMULARIO", 1, true, TemplateApiTests.Form);
        using var next = await client.PostAsJsonAsync($"/api/v1/templates/{created.TemplateId}/versions", new CreateTemplateVersionDto([section2]));
        Assert.Equal(2, (await next.Content.ReadResultAsync<TemplateVersionDto>())!.VersionNumber);
        var versions = (await client.GetResultAsync<TemplateVersionDto[]>($"/api/v1/templates/{created.TemplateId}/versions"))!;
        Assert.Equal(new[] { 1, 2 }, versions.Select(v => v.VersionNumber));
        Assert.Equal("Información", versions[0].Sections.Single().Title); Assert.Equal("Nueva estructura", versions[1].Sections.Single().Title);
        using var updated = await client.PutAsJsonAsync($"/api/v1/templates/{created.TemplateId}", new UpdateTemplateDto("Nuevo nombre"));
        var metadata = (await updated.Content.ReadResultAsync<TemplateDto>())!;
        Assert.Equal("Nuevo nombre", metadata.Name); Assert.Equal(scopeName, metadata.Scope); Assert.Equal(center, metadata.SolutionCenterCode);
        Assert.Equal(2, metadata.Versions.Count);
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/templates/{created.TemplateId}", new { name = "X", scope = "GLOBAL", solutionCenterCode = "R02", actaTypeId = 3 }), HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task Invalid_complete_template_and_late_database_failure_leave_no_partial_records()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/with-version", Request() with { Sections = [] }), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/with-version", Request() with { Sections = [new("Bad", "FORMULARIO", 1, true, null)] }), HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var template = new Template { ActaTypeId = 1, Name = "Rollback", Scope = "GLOBAL", CreatedByUser = 1 };
        template.Versions.Add(new TemplateVersion { ActaTypeId = 1, VersionNumber = 1, CreatedByUser = 1,
            Sections = [new() { Title = "A", ContentType = "TEXTO", SortOrder = 1 }, new() { Title = "B", ContentType = "TEXTO", SortOrder = 1 }] });
        await Assert.ThrowsAsync<DbUpdateException>(() => new TemplateStore(db).CreateWithVersionAsync(template, default));
        Assert.Empty(await db.Set<Template>().ToArrayAsync()); Assert.Empty(await db.Set<TemplateVersion>().ToArrayAsync()); Assert.Empty(await db.Set<TemplateSection>().ToArrayAsync());
    }
    [Fact]
    public async Task Retired_type_is_never_exposed_even_if_history_prevents_catalog_deletion()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            db.Add(new ActaType { ActaTypeId = 2, Code = "DISCIPLINARIA", Name = "Histórica" }); await db.SaveChangesAsync();
        }
        Assert.Equal(new[] { 1, 3 }, (await client.GetResultAsync<ActaTypeDto[]>("/api/v1/acta-types"))!.Select(t => t.ActaTypeId));
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/templates?actaTypeId=2"), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/with-version", Request() with { ActaTypeId = 2 }), HttpStatusCode.BadRequest);
    }
}
