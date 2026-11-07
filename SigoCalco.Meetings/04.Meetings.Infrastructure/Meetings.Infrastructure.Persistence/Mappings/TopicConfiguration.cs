using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Meetings.Infrastructure.Persistence.Mappings;

public sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("sigocalco_meetings_topic");

        builder.HasKey(x => x.TopicId);

        builder.Property(x => x.TopicId).HasColumnName("topic_id").HasColumnType("bigint").IsRequired().ValueGeneratedOnAdd();
        builder.Property(x => x.MinuteId).HasColumnName("minute_id").HasColumnType("bigint").IsRequired();

        builder.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("int").IsRequired().HasDefaultValue(1);

        builder.HasIndex(x => new { x.MinuteId, x.DisplayOrder }).HasDatabaseName("ix_topic_minute_order");

        builder.HasOne<Minute>().WithMany().HasForeignKey(x => x.MinuteId).HasConstraintName("fk_topic_minute").OnDelete(DeleteBehavior.Restrict);
    }
}
