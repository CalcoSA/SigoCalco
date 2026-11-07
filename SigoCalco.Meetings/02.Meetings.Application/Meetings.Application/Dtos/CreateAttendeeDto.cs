namespace Meetings.Application.Dtos;

public class CreateAttendeeDto
{
    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}