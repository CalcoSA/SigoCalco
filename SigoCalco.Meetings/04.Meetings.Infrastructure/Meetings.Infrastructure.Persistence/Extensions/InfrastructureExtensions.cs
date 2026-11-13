
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Meetings.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;

namespace Meetings.Infrastructure.Persistence.Extensions;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registra los servicios de persistencia
    /// del microservicio Meetings.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MeetingsDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("MeetingsDatabase");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure ConnectionStrings:MeetingsDatabase externamente.");
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 46)));
        });
        services.AddScoped<Meetings.Application.Interfaces.ITemplateStore, TemplateStore>();
        services.AddScoped<Meetings.Application.Interfaces.IActaStore, ActaStore>();
        services.AddScoped<Meetings.Application.Interfaces.IMeetingIntegrationService, Services.MeetingIntegrationService>();
        services.AddSingleton<Meetings.Application.Interfaces.IRoomReservationProvider, Services.SimulatedRoomReservationProvider>();
        services.AddScoped<Meetings.Application.Interfaces.IAuthenticationDirectory, Services.AuthenticationDirectory>();

        return services;
    }
}
