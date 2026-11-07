using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("sigocalco_meetings_meeting");

        builder.HasKey(x => x.MeetingId);

        builder.Property(x => x.MeetingId).HasColumnName("meeting_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();
        builder.Property(x => x.MinuteId).HasColumnName("minute_id").HasColumnType("bigint").IsRequired();

        builder.Property(x => x.Title).HasColumnName("title").HasColumnType("varchar(250)").HasMaxLength(250).IsRequired();
        builder.Property(x => x.StartAt).HasColumnName("start_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.EndAt).HasColumnName("end_at").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.Modality).HasColumnName("modality").HasColumnType("enum('VIRTUAL','PRESENCIAL')").IsRequired();
        builder.Property(x => x.ReservationId).HasColumnName("reservation_id").HasColumnType("bigint");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('SCHEDULED','COMPLETED','CANCELLED')").IsRequired().HasDefaultValue("SCHEDULED");

        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.OutlookEventId).HasColumnName("outlook_event_id").HasColumnType("varchar(512)").HasMaxLength(512);
        builder.Property(x => x.OutlookCalendarId).HasColumnName("outlook_calendar_id").HasColumnType("varchar(255)").HasMaxLength(255);
        builder.Property(x => x.OutlookSyncStatus).HasColumnName("outlook_sync_status").HasColumnType("enum('PENDING','SYNCED','ERROR')").IsRequired().HasDefaultValue("PENDING");

        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => x.MinuteId).IsUnique().HasDatabaseName("uq_meeting_minute");
        builder.HasIndex(x => new { x.StartAt, x.EndAt }).HasDatabaseName("ix_meeting_start_end");
        builder.HasIndex(x => x.ReservationId).HasDatabaseName("ix_meeting_reservation");
        builder.HasIndex(x => x.CreatedByUserId).HasDatabaseName("ix_meeting_created_by");
        builder.HasIndex(x => x.OutlookSyncStatus).HasDatabaseName("ix_meeting_outlook_sync");

        builder.HasOne(x => x.Minute).WithOne().HasForeignKey<Meeting>(x => x.MinuteId).HasConstraintName("fk_meeting_minute").OnDelete(DeleteBehavior.Restrict);

        // ReservationId y CreatedByUserId son referencias entre servicios; no son FK locales.
    }
}
