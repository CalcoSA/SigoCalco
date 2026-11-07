using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MinuteConfiguration : IEntityTypeConfiguration<Minute>
{
    public void Configure(EntityTypeBuilder<Minute> builder)
    {
        builder.ToTable("sigocalco_meetings_minute");

        builder.HasKey(x => x.MinuteId);

        builder.Property(x => x.MinuteId).HasColumnName("minute_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();

        builder.Property(x => x.MinuteTypeId).HasColumnName("minute_type_id").HasColumnType("bigint");
        builder.Property(x => x.TemplateId).HasColumnName("template_id").HasColumnType("bigint");
        builder.Property(x => x.Title).HasColumnName("title").HasColumnType("varchar(250)").HasMaxLength(250).IsRequired();
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on").HasColumnType("date");
        builder.Property(x => x.ActualStartTime).HasColumnName("actual_start_time").HasColumnType("time");
        builder.Property(x => x.ActualEndTime).HasColumnName("actual_end_time").HasColumnType("time");
        builder.Property(x => x.Modality).HasColumnName("modality").HasColumnType("enum('VIRTUAL','PRESENCIAL')");
        builder.Property(x => x.LocationDescription).HasColumnName("location_description").HasColumnType("varchar(255)").HasMaxLength(255);
        builder.Property(x => x.MeetingKind).HasColumnName("meeting_kind").HasColumnType("enum('ORDINARIA','EXTRAORDINARIA')");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('DRAFT','CLOSED')").IsRequired().HasDefaultValue("DRAFT");

        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAddOrUpdate();

        builder.Property(x => x.ClosedAt).HasColumnName("closed_at").HasColumnType("datetime(6)");

        builder.HasIndex(x => x.MinuteTypeId).HasDatabaseName("ix_minute_type");
        builder.HasIndex(x => x.TemplateId).HasDatabaseName("ix_minute_template");
        builder.HasIndex(x => x.OccurredOn).HasDatabaseName("ix_minute_date");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_minute_created_by");

        builder.HasOne<MinuteType>().WithMany().HasForeignKey(x => x.MinuteTypeId).HasConstraintName("fk_minute_type").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MinuteTemplate>().WithMany().HasForeignKey(x => x.TemplateId).HasConstraintName("fk_minute_template").OnDelete(DeleteBehavior.Restrict);
    }
}
