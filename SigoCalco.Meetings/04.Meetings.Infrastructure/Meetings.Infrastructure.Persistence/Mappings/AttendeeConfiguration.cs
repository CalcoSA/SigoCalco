using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable("sigocalco_meetings_attendee");

        builder.HasKey(x => x.AttendeeId);

        builder.Property(x => x.AttendeeId).HasColumnName("attendee_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();

        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("bigint");
        builder.Property(x => x.FullName).HasColumnName("full_name").HasColumnType("varchar(200)").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("varchar(254)").HasMaxLength(254).IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("tinyint(1)").IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_attendee_email");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_attendee_user");
    }
}
