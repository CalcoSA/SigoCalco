
namespace Meetings.Application.Dtos;

/// <summary>
/// Resultado de la creación de una reunión.
/// </summary>
public class CreateMeetingResponseDto
{
    public long MeetingId { get; set; }

    public long MinuteId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string OutlookSyncStatus { get; set; } = string.Empty;
}
