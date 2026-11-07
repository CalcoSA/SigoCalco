using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class CommitmentConfiguration : IEntityTypeConfiguration<Commitment>
{
    public void Configure(EntityTypeBuilder<Commitment> builder)
    {
        builder.ToTable("sigocalco_meetings_commitment");

        builder.HasKey(x => x.CommitmentId);

        builder.Property(x => x.CommitmentId).HasColumnName("commitment_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();
        builder.Property(x => x.MinuteId).HasColumnName("minute_id").HasColumnType("bigint").IsRequired();

        builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(x => x.ResponsibleAttendeeId).HasColumnName("responsible_attendee_id").HasColumnType("bigint");
        builder.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
        builder.Property(x => x.Status).HasColumnName("status").HasColumnType("enum('PENDING','IN_PROGRESS','COMPLETED')").IsRequired().HasDefaultValue("PENDING");

        builder.Property(x => x.CompletedAt).HasColumnName("completed_at").HasColumnType("datetime(6)");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired().HasDefaultValueSql("CURRENT_TIMESTAMP(6)").ValueGeneratedOnAdd();

        builder.HasIndex(x => x.MinuteId).HasDatabaseName("ix_commitment_minute");
        builder.HasIndex(x => new { x.ResponsibleAttendeeId, x.Status }).HasDatabaseName("ix_commitment_responsible_status");
        builder.HasIndex(x => x.DueDate).HasDatabaseName("ix_commitment_due_date");

        builder.HasOne<Minute>().WithMany().HasForeignKey(x => x.MinuteId).HasConstraintName("fk_commitment_minute").OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attendee>().WithMany().HasForeignKey(x => x.ResponsibleAttendeeId).HasConstraintName("fk_commitment_attendee").OnDelete(DeleteBehavior.Restrict);
    }
}
