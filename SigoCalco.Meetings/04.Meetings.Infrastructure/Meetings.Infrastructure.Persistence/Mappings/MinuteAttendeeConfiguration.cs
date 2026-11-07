using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class MinuteAttendeeConfiguration : IEntityTypeConfiguration<MinuteAttendee>
{
    public void Configure(EntityTypeBuilder<MinuteAttendee> builder)
    {
        builder.ToTable("sigocalco_meetings_minute_attendee");

        builder.HasKey(x => new { x.MinuteId, x.AttendeeId });

        builder.Property(x => x.MinuteId).HasColumnName("minute_id").HasColumnType("bigint").IsRequired();

        builder.Property(x => x.AttendeeId).HasColumnName("attendee_id").HasColumnType("bigint").IsRequired();

        builder.HasIndex(x => x.AttendeeId).HasDatabaseName("ix_minute_attendee_attendee");

        builder.HasOne<Minute>().WithMany().HasForeignKey(x => x.MinuteId).HasConstraintName("fk_minute_attendee_minute").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attendee>().WithMany().HasForeignKey(x => x.AttendeeId).HasConstraintName("fk_minute_attendee_attendee").OnDelete(DeleteBehavior.Restrict);
    }
}
