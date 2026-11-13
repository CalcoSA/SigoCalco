namespace Meetings.Domain.Exceptions;

public sealed class MeetingsRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public static MeetingsRequestException Invalid(string message) => new(400, message);
    public static MeetingsRequestException Forbidden() => new(403, "No tiene permiso para esta operación.");
    public static MeetingsRequestException NotFound(string message) => new(404, message);
}

