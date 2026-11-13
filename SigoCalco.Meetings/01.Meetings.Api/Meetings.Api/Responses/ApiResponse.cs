namespace Meetings.Api.Responses;

public sealed record ApiResponse<T>(bool IsSuccess, string Message, T? Result);

public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T result, string message = "Operación realizada correctamente.") => new(true, message, result);
    public static ApiResponse<object> Failure(string message) => new(false, message, null);
}
