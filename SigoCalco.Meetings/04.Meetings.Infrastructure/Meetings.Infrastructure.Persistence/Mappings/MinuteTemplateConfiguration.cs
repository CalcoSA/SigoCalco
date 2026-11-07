using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MinuteTemplateConfiguration : IEntityTypeConfiguration<MinuteTemplate>
{
    public void Configure(EntityTypeBuilder<MinuteTemplate> builder)
    {
        builder.ToTable("sigocalco_meetings_minute_template");

        builder.HasKey(x => x.TemplateId);

        builder.Property(x => x.TemplateId).HasColumnName("template_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("varchar(150)").HasMaxLength(150).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("uq_minute_template_name");
    }
}
