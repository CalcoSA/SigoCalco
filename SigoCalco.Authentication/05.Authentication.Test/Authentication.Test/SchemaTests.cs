using System.Text.RegularExpressions;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SigoCalco.Authentication.Test;

public class SchemaTests
{
    [Fact]
    public void EverySqlColumnKeyIndexAndRelationshipMatchesTheMysqlModel()
    {
        using var context = new SigoCalcoContext(new DbContextOptionsBuilder<SigoCalcoContext>()
            .UseMySql("Server=localhost;Database=bdcalco_sigocalco;",
                new MySqlServerVersion(new Version(8, 0, 36))).Options);
        var generated = Tables(context.Database.GenerateCreateScript());
        var manual = Tables(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CreateSigoCalcoAuthentication.sql")));
        Assert.Equal(8, manual.Count);
        Assert.Equal(generated.Keys.Order(), manual.Keys.Order());
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var name = entity.GetTableName()!;
            var table = StoreObjectIdentifier.Table(name, null);
            var expectedColumns = Columns(generated[name]);
            var actualColumns = Columns(manual[name]);
            Assert.Equal(expectedColumns.Keys.Order(), actualColumns.Keys.Order());
            foreach (var column in expectedColumns)
                Assert.True(column.Value == actualColumns[column.Key],
                    $"{name}.{column.Key}: model = {column.Value}; SQL = {actualColumns[column.Key]}");

            var body = Normalize(manual[name]);
            string Fields(IEnumerable<IProperty> properties, StoreObjectIdentifier target)
                => string.Join(", ", properties.Select(p => $"`{p.GetColumnName(target)}`"));
            Assert.Contains(Normalize($"PRIMARY KEY ({Fields(entity.FindPrimaryKey()!.Properties, table)})"), body);
            Assert.Equal(entity.GetIndexes().Count(), Regex.Matches(body, @"(?:UNIQUE )?KEY `").Count);
            foreach (var index in entity.GetIndexes())
                Assert.Contains(Normalize($"{(index.IsUnique ? "UNIQUE " : "")}KEY `{index.GetDatabaseName()}` ({Fields(index.Properties, table)})"), body);
            Assert.Equal(entity.GetForeignKeys().Count(), Regex.Matches(body, "FOREIGN KEY").Count);
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                var parentName = foreignKey.PrincipalEntityType.GetTableName()!;
                var parent = StoreObjectIdentifier.Table(parentName, null);
                var delete = foreignKey.DeleteBehavior switch
                {
                    DeleteBehavior.Cascade => "CASCADE",
                    DeleteBehavior.SetNull => "SET NULL",
                    _ => "RESTRICT"
                };
                Assert.Contains(Normalize($"CONSTRAINT `{foreignKey.GetConstraintName()}` FOREIGN KEY ({Fields(foreignKey.Properties, table)}) " +
                    $"REFERENCES `{parentName}` ({Fields(foreignKey.PrincipalKey.Properties, parent)}) ON DELETE {delete} ON UPDATE RESTRICT"), body);
            }
        }
    }

    private static Dictionary<string, string> Tables(string sql) => Regex.Matches(sql,
        @"CREATE TABLE `(?<name>[^`]+)`\s*\((?<body>.*?)\)\s*(?:ENGINE\s*=.*?|CHARACTER SET\s*=.*?)?;",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)
        .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["body"].Value);

    private static Dictionary<string, string> Columns(string body) => Regex.Matches(body,
        @"^\s*`(?<name>[^`]+)`\s+(?<definition>[^\r\n]+)", RegexOptions.Multiline)
        .ToDictionary(match => match.Groups["name"].Value, match => Normalize(match.Groups["definition"].Value.TrimEnd(',')));

    private static string Normalize(string sql) => Regex.Replace(sql.ToUpperInvariant()
        .Replace("CHARACTER SET UTF8MB4", "") // Inherits the identical table charset.
        .Replace("DEFAULT NULL", "")
        .Replace("DEFAULT '1'", "DEFAULT 1")
        .Replace("DEFAULT TRUE", "DEFAULT 1"), @"\s+", " ").Trim();

    [Fact]
    public void ManualSqlCoversTheMysqlModelWithoutTouchingOtherServices()
    {
        using var context = new SigoCalcoContext(new DbContextOptionsBuilder<SigoCalcoContext>()
            .UseMySql("Server=localhost;Database=bdcalco_sigocalco;",
                new MySqlServerVersion(new Version(8, 0, 36))).Options);
        // Metadata generation only: does not open a database connection.
        var generated = context.Database.GenerateCreateScript();
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CreateSigoCalcoAuthentication.sql"));
        Assert.Contains("USE `bdcalco_sigocalco`;", sql);
        Assert.Equal(8, Regex.Matches(sql, "CREATE TABLE").Count);
        Assert.DoesNotContain("sitracalco_", sql);
        Assert.DoesNotContain("ALTER TABLE", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        foreach (var entity in context.Model.GetEntityTypes())
        {
            Assert.StartsWith("sigocalco_authentication_", entity.GetTableName());
            Assert.Contains($"CREATE TABLE `{entity.GetTableName()}`", sql);
            Assert.Contains($"CREATE TABLE `{entity.GetTableName()}`", generated);
            foreach (var foreignKey in entity.GetForeignKeys())
                Assert.Contains($"`{foreignKey.GetConstraintName()}`", sql);
            foreach (var index in entity.GetIndexes())
                Assert.Contains($"`{index.GetDatabaseName()}`", sql);
        }
        var user = context.Model.FindEntityType(typeof(User))!;
        Assert.False(user.FindProperty(nameof(User.Email))!.IsNullable);
        Assert.Equal(254, user.FindProperty(nameof(User.Email))!.GetMaxLength());
        Assert.DoesNotContain(user.GetIndexes(), index => index.IsUnique
            && index.Properties.Any(property => property.Name == nameof(User.Email)));
        Assert.Contains("`email` VARCHAR(254) NOT NULL", sql);
        var userrole = context.Model.FindEntityType(typeof(Userrole))!;
        Assert.False(userrole.FindProperty(nameof(Userrole.SolutionCenterId))!.IsNullable);
        foreach (var parent in new[] { typeof(Role), typeof(SolutionCenter) })
            Assert.Equal(DeleteBehavior.Restrict,
                userrole.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == parent).DeleteBehavior);
        var assignmentSql = sql[sql.IndexOf("CREATE TABLE `sigocalco_authentication_userrole`", StringComparison.Ordinal)..];
        Assert.Contains("`solution_center_id` INT NOT NULL", assignmentSql);
        Assert.Contains("REFERENCES `sigocalco_authentication_role` (`IdRole`)\n        ON DELETE RESTRICT", assignmentSql);
    }
}
