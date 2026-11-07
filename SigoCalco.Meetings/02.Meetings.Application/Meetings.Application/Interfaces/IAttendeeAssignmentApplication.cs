
namespace Meetings.Application.Interfaces;

public interface IAttendeeAssignmentApplication
{
    Task AddToMeetingAsync(
        long meetingId,
        long attendeeId,
        CancellationToken cancellationToken = default);

    Task AddToMinuteAsync(
        long minuteId,
        long attendeeId,
        CancellationToken cancellationToken = default);
}
