using SigoCalco.Authentication.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class AuthenticationSessionConfiguration : IEntityTypeConfiguration<AuthenticationSession>
    {
        public void Configure(EntityTypeBuilder<AuthenticationSession> entity)
        {
            var utcConverter = new ValueConverter<DateTime, DateTime>(
                value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
            entity.ToTable("sigocalco_authentication_sessions");
            entity.HasKey(x => x.IdSession).HasName("PRIMARY");
            entity.Property(x => x.IdSession).HasColumnName("id_session").HasColumnType("varchar(36)")
                .HasMaxLength(36).UseCollation("ascii_bin").HasCharSet("ascii").ValueGeneratedNever();
            entity.Property(x => x.IdUser).HasColumnName("id_user").HasColumnType("int");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)")
                .HasConversion(utcConverter);
            entity.Property(x => x.LastActivityAt).HasColumnName("last_activity_at").HasColumnType("datetime(6)")
                .HasConversion(utcConverter);
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("datetime(6)")
                .HasConversion(utcConverter);
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("datetime(6)")
                .HasConversion(utcConverter);
            entity.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasColumnType("varchar(64)")
                .HasMaxLength(64).UseCollation("ascii_bin").HasCharSet("ascii");
            entity.HasIndex(x => x.IdUser, "ix_sigocalco_authentication_sessions_user");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.IdUser)
                .HasConstraintName("fk_sigocalco_authentication_sessions_user").OnDelete(DeleteBehavior.Cascade);
        }
    }
}
