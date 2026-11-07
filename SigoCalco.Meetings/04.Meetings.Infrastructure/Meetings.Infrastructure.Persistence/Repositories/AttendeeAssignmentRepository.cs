
using Meetings.Domain.Exceptions;
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;
using Meetings.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Meetings.Infrastructure.Persistence.Repositories;

public class AttendeeAssignmentRepository
    : IAttendeeAssignmentRepository
{
    private readonly MeetingsDbContext _context;

    public AttendeeAssignmentRepository(
        MeetingsDbContext context)
    {
        _context = context;
    }

    public Task AddToMeetingAsync(
        long meetingId,
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        var relation = new MeetingAttendee
        {
            MeetingId = meetingId,
            AttendeeId = attendeeId
        };

        return SaveRelationAsync(
            relation, cancellationToken);
    }

    public Task AddToMinuteAsync(
        long minuteId,
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        var relation = new MinuteAttendee
        {
            MinuteId = minuteId,
            AttendeeId = attendeeId
        };

        return SaveRelationAsync(
            relation, cancellationToken);
    }

    private async Task SaveRelationAsync<T>(
        T relation,
        CancellationToken cancellationToken)
        where T : class
    {
        _context.Set<T>().Add(relation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is MySqlException
            { Number: 1062 })
        {
            _context.Entry(relation).State =
                EntityState.Detached;

            throw new AttendeeAlreadyAssignedException();
        }
    }
}
