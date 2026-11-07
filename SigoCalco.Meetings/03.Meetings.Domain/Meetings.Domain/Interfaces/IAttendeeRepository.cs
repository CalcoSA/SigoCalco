
using Meetings.Domain.Models;

namespace Meetings.Domain.Interfaces;

public interface IAttendeeRepository
{
    Task<Attendee?> GetByIdAsync(
        long attendeeId,
        CancellationToken cancellationToken = default);

    Task<Attendee?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<Attendee> CreateAsync(
        Attendee attendee,
        CancellationToken cancellationToken = default);
}
