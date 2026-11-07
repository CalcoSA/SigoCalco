using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data
{
    public partial class SigoCalcoContext : DbContext
    {
        public SigoCalcoContext() { }
        public SigoCalcoContext(DbContextOptions<SigoCalcoContext> options) : base(options) { }

        public virtual DbSet<Menuoption> Menuoptions { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<Rolemenuoption> Rolemenuoptions { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Userrole> Userroles { get; set; }
        public virtual DbSet<SolutionCenter> SolutionCenters { get; set; }
        public virtual DbSet<AuthenticationLog> AuthenticationLogs { get; set; }
        public virtual DbSet<AuthenticationSession> AuthenticationSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(SigoCalcoContext).Assembly);
        }
    }
}
