
using Meetings.Domain.Exceptions;
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public class AttendeeRepository : IAttendeeRepository
{
    private readonly MeetingsDbContext _context;

    public AttendeeRepository(MeetingsDbContext context)
    {
        _context = context;
    }

    public async Task<Attendee?> GetByIdAsync(
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Attendees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AttendeeId == attendeeId,
                cancellationToken);
    }

    public async Task<Attendee?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _context.Attendees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Email == email,
                cancellationToken);
    }

    public async Task<Attendee> CreateAsync(
        Attendee attendee,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attendee);

        if (attendee.AttendeeId != 0)
        {
            throw new ArgumentException(
                "El asistente debe ser nuevo.");
        }

        _context.Attendees.Add(attendee);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException
            { Number: 1062 })
        {
            _context.Entry(attendee).State =
                EntityState.Detached;

            throw new AttendeeAlreadyExistsException();
        }

        return attendee;
    }
}
