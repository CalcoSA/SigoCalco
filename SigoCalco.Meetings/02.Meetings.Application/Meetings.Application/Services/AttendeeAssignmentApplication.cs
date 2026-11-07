
using Meetings.Application.Interfaces;
using Meetings.Domain.Interfaces;

namespace Meetings.Application.Services;

public class AttendeeAssignmentApplication
    : IAttendeeAssignmentApplication
{
    private readonly IAttendeeAssignmentRepository _assignmentRepository;
    private readonly IAttendeeRepository _attendeeRepository;
    private readonly IMeetingRepository _meetingRepository;
    private readonly IMinuteRepository _minuteRepository;

    public AttendeeAssignmentApplication(
        IAttendeeAssignmentRepository assignmentRepository,
        IAttendeeRepository attendeeRepository,
        IMeetingRepository meetingRepository,
        IMinuteRepository minuteRepository)
    {
        _assignmentRepository = assignmentRepository;
        _attendeeRepository = attendeeRepository;
        _meetingRepository = meetingRepository;
        _minuteRepository = minuteRepository;
    }

    public async Task AddToMeetingAsync(
        long meetingId,
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(meetingId, attendeeId);

        var meeting = await _meetingRepository.GetByIdAsync(
            meetingId, cancellationToken);

        if (meeting is null)
        {
            throw new KeyNotFoundException(
                "No se encontró la reunión.");
        }

        await ValidateAttendeeAsync(
            attendeeId, cancellationToken);

        await _assignmentRepository.AddToMeetingAsync(
            meetingId, attendeeId, cancellationToken);
    }

    public async Task AddToMinuteAsync(
        long minuteId,
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        ValidateIds(minuteId, attendeeId);

        var minute = await _minuteRepository.GetByIdAsync(
            minuteId, cancellationToken);

        if (minute is null)
        {
            throw new KeyNotFoundException(
                "No se encontró el acta.");
        }

        await ValidateAttendeeAsync(
            attendeeId, cancellationToken);

        await _assignmentRepository.AddToMinuteAsync(
            minuteId, attendeeId, cancellationToken);
    }

    private async Task ValidateAttendeeAsync(
        long attendeeId,
        CancellationToken cancellationToken)
    {
        var attendee = await _attendeeRepository.GetByIdAsync(
            attendeeId, cancellationToken);

        if (attendee is null)
        {
            throw new KeyNotFoundException(
                "No se encontró el asistente.");
        }

        if (!attendee.IsActive)
        {
            throw new InvalidOperationException(
                "No se puede asociar un asistente inactivo.");
        }
    }

    private static void ValidateIds(
        long recordId,
        long attendeeId)
    {
        if (recordId <= 0 || attendeeId <= 0)
        {
            throw new ArgumentException(
                "Los identificadores deben ser mayores que cero.");
        }
    }
}
