
using Meetings.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Data;

public class MeetingsDbContext : DbContext
{
    public MeetingsDbContext(
        DbContextOptions<MeetingsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Minute> Minutes => Set<Minute>();

    public DbSet<Meeting> Meetings => Set<Meeting>();

    public DbSet<MinuteType> MinuteTypes => Set<MinuteType>();

    public DbSet<MinuteTemplate> MinuteTemplates => Set<MinuteTemplate>();

    public DbSet<Attendee> Attendees => Set<Attendee>();

    public DbSet<MeetingAttendee> MeetingAttendees =>
        Set<MeetingAttendee>();

    public DbSet<MinuteAttendee> MinuteAttendees =>
        Set<MinuteAttendee>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<Commitment> Commitments => Set<Commitment>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MeetingsDbContext).Assembly);
    }
}
