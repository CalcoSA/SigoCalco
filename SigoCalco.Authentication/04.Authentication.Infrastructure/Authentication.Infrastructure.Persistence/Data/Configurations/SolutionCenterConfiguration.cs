using SigoCalco.Authentication.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data.Configurations
{
    public class SolutionCenterConfiguration : IEntityTypeConfiguration<SolutionCenter>
    {
        public void Configure(EntityTypeBuilder<SolutionCenter> entity)
        {
            entity.ToTable("sigocalco_authentication_solution_center");
            entity.HasKey(e => e.SolutionCenterId).HasName("PRIMARY");
            entity.Property(e => e.SolutionCenterId)
                .HasColumnName("solution_center_id").HasColumnType("int")
                .ValueGeneratedOnAdd();
            entity.Property(e => e.SolutionCenterCode)
                .HasColumnName("solution_center_code").HasColumnType("varchar(64)")
                .HasMaxLength(64).IsRequired();
            entity.Property(e => e.SolutionCenterName)
                .HasColumnName("solution_center_name").HasColumnType("varchar(200)")
                .HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.SolutionCenterCode, "uq_sigocalco_authentication_center_code").IsUnique();
        }
    }
}
