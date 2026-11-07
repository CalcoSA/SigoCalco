using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class UserroleConfiguration : IEntityTypeConfiguration<Userrole>
    {
        public void Configure(EntityTypeBuilder<Userrole> entity)
        {
            entity.HasKey(e => e.IdUserRole).HasName("PRIMARY");

            entity.ToTable("sigocalco_authentication_userrole");

            entity.HasIndex(e => e.IdRole, "fk_sigocalco_authentication_user_role_role");

            entity.HasIndex(e => new { e.IdUser, e.IdRole }, "uq_sigocalco_authentication_user_role").IsUnique();

            entity.Property(e => e.SolutionCenterId)
                .HasColumnName("solution_center_id")
                .HasColumnType("int").IsRequired();
            entity.HasIndex(e => e.SolutionCenterId, "ix_sigocalco_authentication_userrole_center");
            entity.HasOne(e => e.SolutionCenter).WithMany()
                .HasForeignKey(e => e.SolutionCenterId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sigocalco_authentication_userrole_center");

            entity.HasOne(d => d.IdRoleNavigation).WithMany(p => p.Userroles)
                .HasForeignKey(d => d.IdRole)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_sigocalco_authentication_user_role_role");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.Userroles)
                .HasForeignKey(d => d.IdUser)
                .HasConstraintName("fk_sigocalco_authentication_user_role_user");
        }
    }
}
