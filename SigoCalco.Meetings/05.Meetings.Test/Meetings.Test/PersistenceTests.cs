using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meetings.Test;

public sealed class PersistenceTests
{
    private static async Task<Template> Template(MeetingsDbContext db)
    {
        var template = new Template { ActaTypeId = 1, Name = "Prueba", Scope = "GLOBAL", CreatedByUser = 1, CreatedAt = DateTime.UtcNow };
        db.Add(template);
        await db.SaveChangesAsync();
        return template;
    }
    private static TemplateVersion Version(int id) => new()
    {
        TemplateId = id, ActaTypeId = 1, CreatedByUser = 1, CreatedAt = DateTime.UtcNow,
        Sections = [new TemplateSection { Title = "A", ContentType = "TEXTO", SortOrder = 1 }]
    };

    [Fact]
    public async Task Failed_section_insert_rolls_back_version_and_all_sections()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var template = await Template(db);
        var version = Version(template.TemplateId);
        version.Sections.Add(new TemplateSection { Title = "Duplicate", ContentType = "TEXTO", SortOrder = 1 });
        await Assert.ThrowsAsync<DbUpdateException>(() => new TemplateStore(db).CreateVersionAsync(version, default));
        Assert.Equal(0, await db.Set<TemplateVersion>().CountAsync());
        Assert.Equal(0, await db.Set<TemplateSection>().CountAsync());
        Assert.Equal(1, (await new TemplateStore(db).CreateVersionAsync(Version(template.TemplateId), default)).VersionNumber);
    }

    [Fact]
    public async Task Versions_are_append_only_and_have_unique_numbers()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var template = await Template(db);
        var store = new TemplateStore(db);
        var first = await store.CreateVersionAsync(Version(template.TemplateId), default);
        var second = await store.CreateVersionAsync(Version(template.TemplateId), default);
        Assert.Equal(2, second.VersionNumber);
        first.Sections[0].Title = "Changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Add(new TemplateSection { TemplateVersionId = first.TemplateVersionId, Title = "Extra", ContentType = "TEXTO", SortOrder = 2 });
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var duplicate = Version(template.TemplateId);
        duplicate.VersionNumber = 1;
        db.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Acta_cannot_reference_a_section_from_another_version()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        var template = await Template(db);
        var store = new TemplateStore(db);
        var first = await store.CreateVersionAsync(Version(template.TemplateId), default);
        var second = await store.CreateVersionAsync(Version(template.TemplateId), default);
        db.Add(new ActaStatus { ActaStatusId = 1, Name = "BORRADOR" });
        var acta = new Acta { ActaCode = "TEST-1", ActaTypeId = 1, TemplateVersionId = first.TemplateVersionId,
            ActaStatusId = 1, SolutionCenterCode = "R01", Title = "Prueba", CreatedByUser = 1, DocumentDate = new DateOnly(2026,11,10) };
        db.Add(acta);
        await db.SaveChangesAsync();
        db.Add(new ActaSection { ActaId = acta.ActaId, TemplateVersionId = first.TemplateVersionId, TemplateSectionId = second.Sections[0].TemplateSectionId });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Meeting_requires_an_acta_but_acta_can_exist_without_meeting()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        db.Add(new MeetingStatus { MeetingStatusId = 1, Name = "PROGRAMADA" });
        await db.SaveChangesAsync();
        db.Add(new ScheduledMeeting { ActaId = 999, MeetingStatusId = 1, OrganizerUserId = 1,
            Modality = "VIRTUAL", StartAtUtc = DateTime.UtcNow, EndAtUtc = DateTime.UtcNow.AddHours(1) });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

