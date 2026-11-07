using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MeetingAttendeeConfiguration : IEntityTypeConfiguration<MeetingAttendee>
{
    public void Configure(EntityTypeBuilder<MeetingAttendee> builder)
    {
        builder.ToTable("sigocalco_meetings_meeting_attendee");

        builder.HasKey(x => new { x.MeetingId, x.AttendeeId });

        builder.Property(x => x.MeetingId).HasColumnName("meeting_id").HasColumnType("bigint").IsRequired();
        builder.Property(x => x.AttendeeId).HasColumnName("attendee_id").HasColumnType("bigint").IsRequired();

        builder.HasIndex(x => x.AttendeeId).HasDatabaseName("ix_meeting_attendee_attendee");

        builder.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).HasConstraintName("fk_meeting_attendee_meeting").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attendee>().WithMany().HasForeignKey(x => x.AttendeeId).HasConstraintName("fk_meeting_attendee_attendee").OnDelete(DeleteBehavior.Restrict);
    }
}
