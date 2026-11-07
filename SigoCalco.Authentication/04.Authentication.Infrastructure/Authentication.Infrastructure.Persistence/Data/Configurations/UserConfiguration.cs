using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> entity)
        {
            entity.HasKey(e => e.IdUser).HasName("PRIMARY");

            entity.ToTable("sigocalco_authentication_users");

            entity.HasIndex(e => e.UserLogin, "uq_sigocalco_authentication_users_user_login").IsUnique();

            entity.HasIndex(e => e.WordpressUserId, "uq_sigocalco_authentication_users_user_wordpress_id").IsUnique();

            entity.Property(e => e.StatusUser)
                .IsRequired()
                .HasDefaultValue(true)
                .HasColumnName("statusUser");
            entity.Property(e => e.UserLogin)
                .IsRequired()
                .HasMaxLength(60)
                .HasColumnName("userLogin");
            entity.Property(e => e.UserName)
                .HasMaxLength(250)
                .HasColumnName("userName");
            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.WordpressUserId).HasColumnName("wordpressUserId");
        }
    }
}
