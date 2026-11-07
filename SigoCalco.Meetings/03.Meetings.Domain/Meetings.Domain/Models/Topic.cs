namespace Meetings.Domain.Models;

public class Topic
{
    public long TopicId { get; set; }

    public long MinuteId { get; set; }

    public string Content { get; set; } = string.Empty;

    public int DisplayOrder { get; set; } = 1;
}