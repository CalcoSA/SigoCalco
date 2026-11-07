
using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public interface IMinuteApplication
{
    Task<MinuteResponseDto> CreateAsync(
        CreateMinuteDto request,
        long createdByUserId,
        CancellationToken cancellationToken = default);

    Task<MinuteResponseDto?> GetByIdAsync(
        long minuteId,
        CancellationToken cancellationToken = default);
}
