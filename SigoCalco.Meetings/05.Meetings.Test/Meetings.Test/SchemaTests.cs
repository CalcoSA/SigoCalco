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
    public void Runtime_model_contains_only_the_17_current_tables()
    {
        using var db = new Meetings.Infrastructure.Persistence.Data.MeetingsDbContext(
            new DbContextOptionsBuilder<Meetings.Infrastructure.Persistence.Data.MeetingsDbContext>()
                .UseMySql("Server=127.0.0.1;Database=unused;", new MySqlServerVersion(new Version(8,0,46))).Options);
        Assert.Equal(17, db.Model.GetEntityTypes().Count());
        Assert.Equal("sigocalco_meetings_meeting", db.Model.FindEntityType(typeof(ScheduledMeeting))!.GetTableName());
        Assert.All(db.Model.GetEntityTypes(), e => Assert.StartsWith("sigocalco_meetings_", e.GetTableName()));
    }

    private static ActasSchemaContext Context() => new(new DbContextOptionsBuilder<ActasSchemaContext>()
        .UseMySql("Server=127.0.0.1;Database=unused;", new MySqlServerVersion(new Version(8,0,46))).Options);

    [Fact]
    public void Schema_is_Meetings_only_and_has_internal_relational_guards()
    {
        using var db = Context();
        Assert.Equal(17, db.Model.GetEntityTypes().Count());
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
            Assert.Equal(sql.Trim(), File.ReadAllText(file).Replace("\r\n", "\n").Trim());
        }
        Assert.DoesNotContain("authentication", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER DATABASE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(17, Regex.Matches(sql, "CREATE TABLE").Count);
    }
}

