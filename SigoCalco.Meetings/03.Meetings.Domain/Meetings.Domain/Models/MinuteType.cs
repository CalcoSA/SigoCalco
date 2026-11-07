namespace Meetings.Domain.Models;

public class MinuteType
{
    public long MinuteTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}