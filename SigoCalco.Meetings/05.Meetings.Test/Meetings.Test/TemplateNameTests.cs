using System.Net;
using System.Net.Http.Json;
using Meetings.Application.Dtos;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meetings.Test;

public sealed class TemplateNameTests
{
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task Names_are_global_across_both_creation_flows_scopes_and_types(bool firstComplete, bool secondComplete)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var first = firstComplete
            ? await client.PostAsJsonAsync("/api/v1/templates/with-version", TemplateManagementTests.Request() with { Name="Nombre Único" })
            : await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1,"Nombre Único","GLOBAL",null));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var second = secondComplete
            ? await client.PostAsJsonAsync("/api/v1/templates/with-version", TemplateManagementTests.Request("CENTRO","R01") with { Name="  NOMBRE ÚNICO  ", ActaTypeId=3 })
            : await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(3,"  NOMBRE ÚNICO  ","CENTRO","R01"));
        await ActaApiTests.AssertFailure(second, HttpStatusCode.Conflict);
        Assert.Contains("Ya existe una plantilla", await second.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(1, await db.Set<Template>().CountAsync());
        Assert.Equal(firstComplete ? 1 : 0, await db.Set<TemplateVersion>().CountAsync());
        Assert.Equal(firstComplete ? 1 : 0, await db.Set<TemplateSection>().CountAsync());
    }

    [Fact]
    public async Task Rename_checks_other_templates_but_same_name_and_new_versions_remain_allowed()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var one = await client.PostAsJsonAsync("/api/v1/templates/with-version", TemplateManagementTests.Request() with { Name="Primera" });
        using var two = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1,"Segunda","GLOBAL",null));
        var template = (await one.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/templates/{template.TemplateId}", new UpdateTemplateDto(" SEGUNDA ")), HttpStatusCode.Conflict);
        using var same = await client.PutAsJsonAsync($"/api/v1/templates/{template.TemplateId}", new UpdateTemplateDto(" PRIMERA "));
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        using var version = await client.PostAsJsonAsync($"/api/v1/templates/{template.TemplateId}/versions", new CreateTemplateVersionDto(TemplateManagementTests.Request().Sections));
        Assert.Equal(HttpStatusCode.Created, version.StatusCode);
        Assert.Equal(2, (await version.Content.ReadResultAsync<TemplateVersionDto>())!.VersionNumber);
    }

    [Fact]
    public async Task Database_generated_key_protects_against_writers_that_skip_the_service()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var first = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1,"Unicidad","GLOBAL",null));
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        db.Add(new Template { Name=" UNICIDAD ", ActaTypeId=1, Scope="CENTRO", SolutionCenterCode="R02", CreatedByUser=9 });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
