
using Meetings.Domain.Models;


namespace Meetings.Domain.Interfaces;

public interface IAttendeeAssignmentRepository
{
    /// <summary>
    /// Relaciona un asistente con una reunión.
    /// </summary>
    Task AddToMeetingAsync(
        long meetingId,
        long attendeeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relaciona un asistente con un acta.
    /// </summary>
    Task AddToMinuteAsync(
        long minuteId,
        long attendeeId,
        CancellationToken cancellationToken = default);


    Task<IReadOnlyList<Attendee>> GetMeetingAttendeesAsync(
        long meetingId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Attendee>> GetMinuteAttendeesAsync(
        long minuteId,
        CancellationToken cancellationToken = default);




}
