using Meetings.Domain.Exceptions;
using Meetings.Api.Responses;
using Microsoft.AspNetCore.Diagnostics;

namespace Meetings.Api.Security;

public sealed class MeetingsExceptionHandler(ILogger<MeetingsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is MeetingsRequestException request)
        {
            context.Response.StatusCode = request.StatusCode;
            await context.Response.WriteAsJsonAsync(ApiResponse.Failure(request.Message), ct);
        }
        else
        {
            // Do not serialize exception details or connection configuration to clients.
            logger.LogError("Meetings request failed ({ExceptionType}).", exception.GetType().Name);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse.Failure("No se pudo completar la operación."), ct);
        }
        return true;
    }
}

