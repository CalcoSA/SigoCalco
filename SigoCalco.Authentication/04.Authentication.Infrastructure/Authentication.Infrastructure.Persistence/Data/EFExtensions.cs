using SigoCalco.Authentication.Infrastructure.Persistence.Repositories;
using SigoCalco.Authentication.Infrastructure.Persistence.Mappings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SigoCalco.Authentication.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Data
{
    public static class EFExtensions
    {
        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("SigoCalcoDatabase");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No se encontró la cadena de conexión 'SigoCalcoDatabase'.");
            }

            if (new MySqlConnectionStringBuilder(connectionString).Database != "bdcalco_sigocalco")
                throw new InvalidOperationException("SigoCalcoDatabase debe apuntar a bdcalco_sigocalco.");

            services.AddDbContext<SigoCalcoContext>(options =>
            {
                options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
            });

            services.AddAutoMapper(config =>
            {
                config.AddProfile<AutoMapperProfile>();
            });

            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
            services.AddScoped<IMenuOptionRepository, MenuOptionRepository>();
            services.AddScoped<IRoleRepository, RoleRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ISolutionCenterRepository, SolutionCenterRepository>();
            services.AddScoped<IWordpressUserRepository, WordpressUserRepository>();
            services.AddScoped<ILogRepository, LogRepository>();
            services.AddScoped<ISessionRepository, SessionRepository>();

            return services;
        }
    }
}
