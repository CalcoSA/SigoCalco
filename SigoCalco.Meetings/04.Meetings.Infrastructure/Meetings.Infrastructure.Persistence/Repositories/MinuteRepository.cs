
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Repositories;

public class MinuteRepository : IMinuteRepository
{
    private readonly MeetingsDbContext _context;

    public MinuteRepository(MeetingsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Consulta un acta por su identificador.
    /// </summary>
    public async Task<Minute?> GetByIdAsync(
        long minuteId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Minutes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.MinuteId == minuteId,
                cancellationToken);
    }


    public async Task<Minute> CreateAsync(
        Minute minute,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(minute);

        if (minute.MinuteId != 0)
        {
            throw new InvalidOperationException(
                "El acta debe ser nueva.");
        }

        _context.Minutes.Add(minute);

        await _context.SaveChangesAsync(cancellationToken);

        return minute;
    }

}
