using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace SigoCalco.Authentication.Test
{
    public class SessionPersistenceTests
    {
        [Fact]
        public void DateConvertersPreserveTheMysqlProviderTypes()
        {
            var builder = new ModelBuilder();
            new AuthenticationSessionConfiguration().Configure(builder.Entity<AuthenticationSession>());
            var entity = builder.Model.FindEntityType(typeof(AuthenticationSession))!;
            foreach (var name in new[] { nameof(AuthenticationSession.CreatedAt), nameof(AuthenticationSession.LastActivityAt), nameof(AuthenticationSession.ExpiresAt) })
                Assert.Equal(typeof(DateTime), entity.FindProperty(name)!.GetValueConverter()!.ProviderClrType);
            Assert.Equal(typeof(DateTime), entity.FindProperty(nameof(AuthenticationSession.RevokedAt))!.GetValueConverter()!.ProviderClrType);
        }

        [Fact]
        public void MysqlSupportsEverySessionColumnType()
        {
            using var context = CreateContext();
            var mappings = context.GetService<IRelationalTypeMappingSource>();
            var builder = new ModelBuilder();
            new AuthenticationSessionConfiguration().Configure(builder.Entity<AuthenticationSession>());
            foreach (var property in builder.Model.FindEntityType(typeof(AuthenticationSession))!.GetProperties())
            {
                var type = property.GetValueConverter()?.ProviderClrType ?? property.ClrType;
                var mapping = mappings.FindMapping(type, property.GetColumnType());
                Assert.True(mapping is not null, $"No mapping for {property.Name}: {type}, {property.GetColumnType()}");
            }
        }

        [Fact]
        public void SessionSchemaHasTheRequiredKeysAndMysqlColumnTypes()
        {
            using var context = CreateContext();
            var entity = context.Model.FindEntityType(typeof(AuthenticationSession))!;
            var table = StoreObjectIdentifier.Table("sigocalco_authentication_sessions", null);

            Assert.Equal(table.Name, entity.GetTableName());
            Assert.Equal(nameof(AuthenticationSession.IdSession), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
            AssertColumn(nameof(AuthenticationSession.IdSession), "id_session", "varchar(36)", false);
            AssertColumn(nameof(AuthenticationSession.IdUser), "id_user", "int", false);
            AssertColumn(nameof(AuthenticationSession.CreatedAt), "created_at", "datetime(6)", false);
            AssertColumn(nameof(AuthenticationSession.LastActivityAt), "last_activity_at", "datetime(6)", false);
            AssertColumn(nameof(AuthenticationSession.ExpiresAt), "expires_at", "datetime(6)", false);
            AssertColumn(nameof(AuthenticationSession.RevokedAt), "revoked_at", "datetime(6)", true);
            AssertColumn(nameof(AuthenticationSession.RefreshTokenHash), "refresh_token_hash", "varchar(64)", true);
            Assert.Equal(36, entity.FindProperty(nameof(AuthenticationSession.IdSession))!.GetMaxLength());
            Assert.Equal(64, entity.FindProperty(nameof(AuthenticationSession.RefreshTokenHash))!.GetMaxLength());
            var foreignKey = Assert.Single(entity.GetForeignKeys());
            Assert.Equal(typeof(User), foreignKey.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
            Assert.Contains(entity.GetIndexes(), index => index.Properties.Single().Name == nameof(AuthenticationSession.IdUser));

            void AssertColumn(string property, string column, string type, bool nullable)
            {
                var metadata = entity.FindProperty(property)!;
                Assert.Equal(column, metadata.GetColumnName(table));
                Assert.Equal(type, metadata.GetColumnType());
                Assert.Equal(nullable, metadata.IsNullable);
            }
        }

        [Theory]
        [InlineData(nameof(AuthenticationSession.CreatedAt))]
        [InlineData(nameof(AuthenticationSession.LastActivityAt))]
        [InlineData(nameof(AuthenticationSession.ExpiresAt))]
        [InlineData(nameof(AuthenticationSession.RevokedAt))]
        public void ReloadedSessionDatesKeepTheirUtcMeaning(string propertyName)
        {
            using var context = CreateContext();
            var property = context.Model.FindEntityType(typeof(AuthenticationSession))!.FindProperty(propertyName)!;
            var stored = new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Unspecified);
            var restored = Assert.IsType<DateTime>(property.GetTypeMapping().Converter!.ConvertFromProvider(stored));

            Assert.Equal(DateTimeKind.Utc, restored.Kind);
            Assert.Equal(stored.Ticks, restored.Ticks);
        }

        private static SigoCalcoContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<SigoCalcoContext>()
                .UseMySql("Server=localhost;Database=unused;User ID=unused;Password=unused;",
                    new MySqlServerVersion(new Version(8, 0, 36)))
                .Options;
            return new SigoCalcoContext(options);
        }
    }
}
