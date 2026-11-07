using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MinuteTypeConfiguration : IEntityTypeConfiguration<MinuteType>
{
    public void Configure(EntityTypeBuilder<MinuteType> builder)
    {
        builder.ToTable("sigocalco_meetings_minute_type");

        builder.HasKey(x => x.MinuteTypeId);

        builder.Property(x => x.MinuteTypeId).HasColumnName("minute_type_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_minute_type_name");
    }
}
