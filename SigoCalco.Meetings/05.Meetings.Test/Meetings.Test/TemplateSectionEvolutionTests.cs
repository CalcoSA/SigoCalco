using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Dtos;
using Meetings.Application.Services;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meetings.Test;

public sealed class TemplateSectionEvolutionTests
{
    internal static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    private const string FixedText = "Texto institucional inmutable.";
    internal static CreateTemplateWithVersionDto Request(string name = "Texto y subtítulo") => new(1, name, "GLOBAL", null,
    [
        new("Notas", "TEXTO", 3, false, Json("""{"mode":"EDITABLE","defaultText":"Borrador inicial"}""")),
        new("DESARROLLO DE LA REUNIÓN", "SUBTITULO", 2, false, null),
        new("Introducción", "TEXTO", 1, true, Json("""{"mode":"FIXED","defaultText":"Texto institucional inmutable."}"""))
    ]);

    // Same end-to-end flow runs against SQLite and an isolated migrated MySQL database.
    internal static async Task ExerciseSections(IServiceProvider services, HttpClient client)
    {
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
            if (!await db.Set<ActaStatus>().AnyAsync(s => s.Name == "BORRADOR")) db.Add(new ActaStatus { Name = "BORRADOR" });
            (await db.Set<MeetingsPermission>().SingleAsync(p => p.PermissionKey == MeetingsPermissions.ActasManage)).PermissionValue = "Administrador";
            await db.SaveChangesAsync();
        }
        using var response = await client.PostAsJsonAsync("/api/v1/templates/with-version", Request());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var template = (await response.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
        Assert.Equal(new[] { "TEXTO", "SUBTITULO", "TEXTO" }, template.Sections.Select(s => s.ContentType));
        Assert.Equal(new[] { 1, 2, 3 }, template.Sections.Select(s => s.SortOrder));
        var fixedId = template.Sections[0].TemplateSectionId;
        var subtitleId = template.Sections[1].TemplateSectionId;
        var editableId = template.Sections[2].TemplateSectionId;
        var version = (await client.GetResultAsync<TemplateVersionDetailDto>($"/api/v1/template-versions/{template.TemplateVersionId}"))!;
        Assert.Equal("FIXED", version.Sections[0].ConfigurationJson!.Value.GetProperty("mode").GetString());
        Assert.Null(version.Sections[1].ConfigurationJson);
        var available = (await client.GetResultAsync<PagedAvailableTemplatesDto>("/api/v1/actas/available-templates"))!;
        Assert.Equal("SUBTITULO", available.Items.Single(t => t.TemplateId == template.TemplateId).Sections[1].ContentType);
        var listing = (await client.GetResultAsync<PagedTemplatesDto>("/api/v1/templates"))!;
        Assert.Equal("EDITABLE", listing.Items.Single(t => t.TemplateId == template.TemplateId).Versions[0].Sections[2].ConfigurationJson!.Value.GetProperty("mode").GetString());

        var input = new CreateActaDto(template.TemplateVersionId, "Acta con texto", new(2026, 11, 25), "R01", [], [], null);
        var acta = await MyActasTests.Create(client, input);
        var detail = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{acta.ActaId}"))!;
        Assert.Equal(FixedText, detail.Sections[0].ContentJson!.Value.GetString());
        Assert.Null(detail.Sections[1].ContentJson); Assert.Equal("DESARROLLO DE LA REUNIÓN", detail.Sections[1].Title);
        Assert.Equal("Borrador inicial", detail.Sections[2].ContentJson!.Value.GetString());
        // An API client cannot override FIXED on either create or update, including non-string payloads.
        foreach (var forged in new[] { Json("\"Texto alterado\""), Json("{\"mode\":\"EDITABLE\",\"text\":\"Alterado\"}") })
        {
            var sections = new ActaSectionInput[] { new(fixedId, forged), new(editableId, Json("\"No guardar\"")) };
            await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input with { Sections = sections }), HttpStatusCode.BadRequest);
            await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}", new UpdateActaDto("No guardar", input.DocumentDate, sections)), HttpStatusCode.BadRequest);
        }
        using var updated = await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}", new UpdateActaDto("Editada", input.DocumentDate,
            [new(fixedId, JsonSerializer.SerializeToElement(FixedText)), new(subtitleId, null), new(editableId, Json("\"Escrito por el usuario\""))]));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Escrito por el usuario", (await updated.Content.ReadResultAsync<ActaDetailDto>())!.Sections[2].ContentJson!.Value.GetString());
        // Null cannot erase FIXED; it can be omitted by the frontend entirely.
        using var nullFixed = await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}", new UpdateActaDto("Editada", input.DocumentDate,
            [new(fixedId, null), new(editableId, Json("\"\""))]));
        Assert.Equal(HttpStatusCode.OK, nullFixed.StatusCode);
        var cleared = (await nullFixed.Content.ReadResultAsync<ActaDetailDto>())!;
        Assert.Equal(FixedText, cleared.Sections[0].ContentJson!.Value.GetString()); Assert.Equal("", cleared.Sections[2].ContentJson!.Value.GetString());
        await ActaApiTests.AssertFailure(await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}", new UpdateActaDto("No guardar", input.DocumentDate,
            [new(subtitleId, Json("\"Captura no permitida\""))])), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await ActaApiTests.SendAsync(client, input with { Sections = [new(subtitleId, Json("\"No\""))] }), HttpStatusCode.BadRequest);

        var nextSections = Request().Sections!.Select(s => s.SortOrder == 1
            ? s with { ConfigurationJson = Json("""{"mode":"FIXED","defaultText":"Texto de la versión nueva"}""") } : s).ToArray();
        using var next = await client.PostAsJsonAsync($"/api/v1/templates/{template.TemplateId}/versions", new CreateTemplateVersionDto(nextSections));
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
        Assert.Equal(2, (await next.Content.ReadResultAsync<TemplateVersionDto>())!.VersionNumber);
        var history = (await client.GetResultAsync<TemplateVersionDto[]>($"/api/v1/templates/{template.TemplateId}/versions"))!;
        Assert.Equal(2, history.Length); Assert.Equal("SUBTITULO", history[1].Sections[1].ContentType);
        Assert.Equal(FixedText, history[0].Sections[0].ConfigurationJson!.Value.GetProperty("defaultText").GetString());
        var original = (await client.GetResultAsync<ActaDetailDto>($"/api/v1/actas/{acta.ActaId}"))!;
        Assert.Equal(template.TemplateVersionId, original.TemplateVersionId); Assert.Equal(FixedText, original.Sections[0].ContentJson!.Value.GetString());
        using var check = services.CreateScope(); var persisted = check.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(2, await persisted.Set<ActaSection>().CountAsync(s => s.ActaId == acta.ActaId));
        Assert.False(await persisted.Set<ActaSection>().AnyAsync(s => s.ActaId == acta.ActaId && s.TemplateSectionId == subtitleId));
        Assert.Equal(1, await persisted.Set<Acta>().CountAsync(a => a.TemplateVersionId == template.TemplateVersionId));
    }

    [Fact]
    public async Task Text_modes_subtitle_versions_and_actas_work_end_to_end()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        await ExerciseSections(factory.Services, client);
    }

    [Theory]
    [InlineData("null")] [InlineData("{}")] [InlineData("{\"defaultText\":\"Valor anterior\"}")]
    [InlineData("{\"legacyOption\":true,\"defaultText\":\"Anterior\"}")]
    public async Task Historical_text_is_editable_without_rewriting_configuration(string configuration)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        await ActaApiTests.PrepareAsync(factory, client);
        using var response = await client.PostAsJsonAsync("/api/v1/templates/with-version", Request("Histórica") with
        { Sections = [new("Texto histórico", "TEXTO", 1, false, Json(configuration))] });
        var template = (await response.Content.ReadResultAsync<CreatedTemplateWithVersionDto>())!;
        var sectionId = template.Sections[0].TemplateSectionId;
        var input = new CreateActaDto(template.TemplateVersionId, "Acta histórica", new(2026,11,25), "R01", [new(sectionId, Json("\"Libre\""))], [], null);
        var acta = await MyActasTests.Create(client, input);
        using var edit = await client.PutAsJsonAsync($"/api/v1/actas/{acta.ActaId}", new UpdateActaDto("Editada", input.DocumentDate, [new(sectionId, Json("\"Cambiado\""))]));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal("Cambiado", (await edit.Content.ReadResultAsync<ActaDetailDto>())!.Sections.Single().ContentJson!.Value.GetString());
        var version = (await client.GetResultAsync<TemplateVersionDetailDto>($"/api/v1/template-versions/{template.TemplateVersionId}"))!;
        Assert.Equal(configuration, version.Sections[0].ConfigurationJson?.GetRawText() ?? "null");
    }

    [Theory]
    [InlineData("{\"mode\":\"OTHER\",\"defaultText\":\"X\"}")]
    [InlineData("{\"mode\":\"fixed\",\"defaultText\":\"X\"}")]
    [InlineData("{\"mode\":null,\"defaultText\":\"X\"}")]
    [InlineData("{\"mode\":\"FIXED\"}")]
    [InlineData("{\"mode\":\"FIXED\",\"defaultText\":\" \"}")]
    [InlineData("{\"mode\":\"EDITABLE\"}")]
    [InlineData("{\"mode\":\"EDITABLE\",\"defaultText\":12}")]
    [InlineData("{\"defaultText\":null}")]
    [InlineData("{\"mode\":\"FIXED\",\"mode\":\"EDITABLE\",\"defaultText\":\"X\"}")]
    [InlineData("{\"mode\":\"EDITABLE\",\"defaultText\":\"\",\"extra\":true}")]
    [InlineData("[]")]
    public async Task Invalid_text_configuration_never_creates_partial_templates_or_versions(string configuration)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        using var parent = await client.PostAsJsonAsync("/api/v1/templates", new CreateTemplateDto(1, "Parent", "GLOBAL", null));
        var id = (await parent.Content.ReadResultAsync<CreatedTemplateDto>())!.TemplateId;
        var sections = new CreateTemplateSectionDto[] { new("Texto", "TEXTO", 1, false, Json(configuration)) };
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/with-version", Request() with { Sections = sections }), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync($"/api/v1/templates/{id}/versions", new CreateTemplateVersionDto(sections)), HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        Assert.Equal(1, await db.Set<Template>().CountAsync()); Assert.Empty(await db.Set<TemplateVersion>().ToListAsync());
    }

    [Theory]
    [InlineData(true, "null")] [InlineData(false, "{}")] [InlineData(false, "\"Texto\"")]
    public async Task Subtitle_rejects_capture_configuration_or_required_flag(bool required, string configuration)
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client();
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/with-version", Request() with
        { Sections = [new("Subtítulo", "SUBTITULO", 1, required, Json(configuration))] }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Editable_required_text_requires_a_nonblank_effective_string()
    {
        var validator = new SectionJsonValidator([new TextSectionValidator()]);
        var config = Json("""{"mode":"EDITABLE","defaultText":""}""");
        foreach (var invalid in new[] { "null", "\"\"", "\" \"", "42", "{}" })
            Assert.Throws<Meetings.Domain.Exceptions.MeetingsRequestException>(() => validator.ValidateContent("TEXTO", config, Json(invalid), true));
        validator.ValidateContent("TEXTO", config, Json("\"Contenido\""), true);
        validator.ValidateContent("TEXTO", Json("""{"mode":"EDITABLE","defaultText":"Precarga"}"""), null, true);
    }
}
