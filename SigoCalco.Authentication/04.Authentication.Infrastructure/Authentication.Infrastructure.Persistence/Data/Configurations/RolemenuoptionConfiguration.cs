using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class RolemenuoptionConfiguration : IEntityTypeConfiguration<Rolemenuoption>
    {
        public void Configure(EntityTypeBuilder<Rolemenuoption> entity)
        {
            entity.HasKey(e => e.IdRoleMenuOption).HasName("PRIMARY");

            entity.ToTable("sigocalco_authentication_rolemenuoption");

            entity.HasIndex(e => e.IdMenuOption, "fk_sigocalco_authentication_role_menu_option_menu_option");

            entity.HasIndex(e => new { e.IdRole, e.IdMenuOption }, "uq_sigocalco_authentication_role_menu_option").IsUnique();

            entity.HasOne(d => d.IdMenuOptionNavigation).WithMany(p => p.Rolemenuoptions)
                .HasForeignKey(d => d.IdMenuOption)
                .HasConstraintName("fk_sigocalco_authentication_role_menu_option_menu_option");

            entity.HasOne(d => d.IdRoleNavigation).WithMany(p => p.Rolemenuoptions)
                .HasForeignKey(d => d.IdRole)
                .HasConstraintName("fk_sigocalco_authentication_role_menu_option_role");
        }
    }
}