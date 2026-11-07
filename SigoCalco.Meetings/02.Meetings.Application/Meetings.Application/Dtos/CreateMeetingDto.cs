
namespace Meetings.Application.Dtos;

/// <summary>
/// Datos necesarios para crear una reunión.
/// </summary>
public class CreateMeetingDto
{
    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndAt { get; set; }

    public string Modality { get; set; } = string.Empty;

    public string? MeetingKind { get; set; }
}
