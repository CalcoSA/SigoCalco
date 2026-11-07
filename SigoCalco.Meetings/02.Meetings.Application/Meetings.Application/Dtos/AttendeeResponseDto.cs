namespace Meetings.Application.Dtos;

public class AttendeeResponseDto
{
    public long AttendeeId { get; set; }

    public long? UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}