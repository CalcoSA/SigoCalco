
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace Meetings.Infrastructure.Persistence.Repositories;

public class MeetingRepository : IMeetingRepository
{
    private readonly MeetingsDbContext _context;

    public MeetingRepository(MeetingsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Obtiene una reunión con su acta asociada.
    /// </summary>
    /// <param name="meetingId">
    /// Type: long - Identificador de la reunión.
    /// </param>
    /// <param name="cancellationToken">
    /// Type: CancellationToken - Token de cancelación.
    /// </param>
    /// <returns>
    /// Type: Meeting? - Reunión encontrada o null.
    /// </returns>
    public async Task<Meeting?> GetByIdAsync(
        long meetingId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Meetings
            .AsNoTracking()
            .Include(x => x.Minute)
            .FirstOrDefaultAsync(
                x => x.MeetingId == meetingId,
                cancellationToken);
    }

    /// <summary>
    /// Crea una reunión con su nueva acta
    /// mediante una transacción de base de datos.
    /// </summary>
    /// <param name="meeting">
    /// Type: Meeting - Reunión con el acta asociada.
    /// </param>
    /// <param name="cancellationToken">
    /// Type: CancellationToken - Token de cancelación.
    /// </param>
    /// <returns>
    /// Type: Meeting - Reunión creada.
    /// </returns>
    public async Task<Meeting> CreateWithMinuteAsync(
        Meeting meeting,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentNullException.ThrowIfNull(meeting.Minute);

        // No permite reutilizar reuniones o actas existentes.
        if (meeting.MeetingId != 0 ||
            meeting.MinuteId != 0 ||
            meeting.Minute.MinuteId != 0)
        {
            throw new InvalidOperationException(
                "La reunión debe crearse con un acta nueva.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                cancellationToken);

        // EF Core detecta la relación y crea el acta
        // antes de insertar la reunión.
        _context.Meetings.Add(meeting);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return meeting;
    }
}
