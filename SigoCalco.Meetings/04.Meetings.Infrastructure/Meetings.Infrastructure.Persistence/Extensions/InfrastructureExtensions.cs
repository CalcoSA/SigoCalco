
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Meetings.Domain.Interfaces;
using Meetings.Infrastructure.Persistence.Repositories;

namespace Meetings.Infrastructure.Persistence.Extensions;

public static class InfrastructureExtensions
{
    /// <summary>
    /// Registra los servicios de persistencia
    /// del microservicio Meetings.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            connectionString);

        services.AddDbContext<MeetingsDbContext>(options =>
            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString)));
        // Registro de repositorios
        services.AddScoped<IMinuteRepository, MinuteRepository>();

        services.AddScoped<IMeetingRepository, MeetingRepository>();

        services.AddScoped<IAttendeeRepository,AttendeeRepository>();

        services.AddScoped<IAttendeeAssignmentRepository,AttendeeAssignmentRepository>();

        return services;
    }
}
