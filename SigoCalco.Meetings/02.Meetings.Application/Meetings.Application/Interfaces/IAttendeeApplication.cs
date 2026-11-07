
using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public interface IAttendeeApplication
{
    Task<AttendeeResponseDto> CreateAsync(
        CreateAttendeeDto request,
        CancellationToken cancellationToken = default);

    Task<AttendeeResponseDto?> GetByIdAsync(
        long attendeeId,
        CancellationToken cancellationToken = default);
}
