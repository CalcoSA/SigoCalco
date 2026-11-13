
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Data;

public class MeetingsDbContext : DbContext
{
    public MeetingsDbContext(
        DbContextOptions<MeetingsDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        Mappings.ActasModel.Configure(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectVersionHistory();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectVersionHistory();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectVersionHistory()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is global::Meetings.Domain.Models.Actas.TemplateVersion or global::Meetings.Domain.Models.Actas.TemplateSection &&
                entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Las versiones y sus secciones son inmutables; cree una versión nueva.");
        }
        var newSections = ChangeTracker.Entries<global::Meetings.Domain.Models.Actas.TemplateVersion>()
            .Where(e => e.State == EntityState.Added).SelectMany(e => e.Entity.Sections).ToHashSet();
        if (ChangeTracker.Entries<global::Meetings.Domain.Models.Actas.TemplateSection>()
            .Any(e => e.State == EntityState.Added && !newSections.Contains(e.Entity)))
            throw new InvalidOperationException("No se pueden agregar secciones a una versión existente.");
    }
}
