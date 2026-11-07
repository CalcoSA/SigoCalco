using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class MenuoptionConfiguration : IEntityTypeConfiguration<Menuoption>
    {
        public void Configure(EntityTypeBuilder<Menuoption> entity)
        {
            entity.HasKey(e => e.IdMenuOption).HasName("PRIMARY");

            entity.ToTable("sigocalco_authentication_menuoption");

            entity.HasIndex(e => e.ParentMenuOption, "fk_sigocalco_authentication_menu_parent");

            entity.Property(e => e.NameMenuOption)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("nameMenuOption");
            entity.Property(e => e.OrderMenuOption).HasColumnName("orderMenuOption");
            entity.Property(e => e.ParentMenuOption).HasColumnName("parentMenuOption");
            entity.Property(e => e.PathMenuOption)
                .HasMaxLength(200)
                .HasColumnName("pathMenuOption");
            entity.Property(e => e.StatusMenuOption)
                .HasDefaultValueSql("'1'")
                .HasColumnName("statusMenuOption");

            entity.HasOne(d => d.ParentMenuOptionNavigation).WithMany(p => p.InverseParentMenuOptionNavigation)
                .HasForeignKey(d => d.ParentMenuOption)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_sigocalco_authentication_menu_parent");
        }
    }
}