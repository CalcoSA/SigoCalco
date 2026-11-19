using System.Text.RegularExpressions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Mappings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Meetings.Test;

public sealed class ActasSchemaContext(DbContextOptions<ActasSchemaContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder) => ActasModel.Configure(modelBuilder);
}

public sealed class SchemaTests
{
    [Fact]
    public void Runtime_model_contains_18_Meetings_tables_including_notifications_and_retained_calendar()
    {
        using var db = new Meetings.Infrastructure.Persistence.Data.MeetingsDbContext(
            new DbContextOptionsBuilder<Meetings.Infrastructure.Persistence.Data.MeetingsDbContext>()
                .UseMySql("Server=127.0.0.1;Database=unused;", new MySqlServerVersion(new Version(8,0,46))).Options);
        Assert.Equal(18, db.Model.GetEntityTypes().Count());
        Assert.Equal("sigocalco_meetings_meeting", db.Model.FindEntityType(typeof(ScheduledMeeting))!.GetTableName());
        Assert.All(db.Model.GetEntityTypes(), e => Assert.StartsWith("sigocalco_meetings_", e.GetTableName()));
    }

    private static ActasSchemaContext Context() => new(new DbContextOptionsBuilder<ActasSchemaContext>()
        .UseMySql("Server=127.0.0.1;Database=unused;", new MySqlServerVersion(new Version(8,0,46))).Options);

    [Fact]
    public void Schema_is_Meetings_only_and_has_internal_relational_guards()
    {
        using var db = Context();
        Assert.Equal(18, db.Model.GetEntityTypes().Count());
        Assert.All(db.Model.GetEntityTypes(), e => Assert.StartsWith("sigocalco_meetings_", e.GetTableName()));
        Assert.All(db.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()),
            fk => Assert.StartsWith("sigocalco_meetings_", fk.PrincipalEntityType.GetTableName()));
        var version = db.Model.FindEntityType(typeof(TemplateVersion))!;
        Assert.Contains(version.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["TemplateId", "VersionNumber"]));
        var section = db.Model.FindEntityType(typeof(ActaSection))!;
        Assert.All(section.GetForeignKeys(), fk => Assert.Equal(2, fk.Properties.Count));
        var acta = db.Model.FindEntityType(typeof(Acta))!;
        Assert.Equal(typeof(int), acta.FindProperty("CreatedByUser")!.ClrType);
        Assert.Equal(typeof(int?), acta.FindProperty("UpdatedByUser")!.ClrType);
    }

    [Fact]
    public void Reviewed_SQL_matches_the_actual_EF_model()
    {
        using var db = Context();
        var sql = db.Database.GenerateCreateScript().Replace("\r\n", "\n");
        sql = Regex.Replace(sql, @"^ALTER DATABASE[^;]*;\s*", "", RegexOptions.Multiline);
        sql = sql.Replace(") CHARACTER SET=utf8mb4", ") ENGINE=InnoDB CHARACTER SET=utf8mb4");
        var export = Environment.GetEnvironmentVariable("MEETINGS_EXPORT_SCHEMA");
        if (!string.IsNullOrWhiteSpace(export))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(export)!);
            File.WriteAllText(export, sql);
        }
        else
        {
            var file = Path.Combine(AppContext.BaseDirectory, "sql", "02_create_model.sql");
            Assert.True(File.Exists(file), "Debe generarse y revisar 02_create_model.sql.");
            var incremental = Path.Combine(AppContext.BaseDirectory, "sql", "04_add_meeting_notifications.sql");
            Assert.True(File.Exists(incremental));
            static string[] Statements(string value) => value.Replace("\r\n", "\n").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.Equal(Statements(sql), Statements(File.ReadAllText(file) + "\n" + File.ReadAllText(incremental)));
        }
        Assert.DoesNotContain("authentication", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER DATABASE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(18, Regex.Matches(sql, "CREATE TABLE").Count);
    }
}

