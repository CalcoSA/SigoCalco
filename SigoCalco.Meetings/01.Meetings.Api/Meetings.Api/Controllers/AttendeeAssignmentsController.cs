
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class AttendeeAssignmentsController : ControllerBase
{
    private readonly IAttendeeAssignmentApplication _application;

    public AttendeeAssignmentsController(
        IAttendeeAssignmentApplication application)
    {
        _application = application;
    }

    [HttpPost("Meetings/{meetingId:long}/attendees")]
    public async Task<IActionResult> AddToMeeting(
        long meetingId,
        [FromBody] AddAttendeeDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _application.AddToMeetingAsync(
                meetingId,
                request.AttendeeId,
                cancellationToken);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (AttendeeAlreadyAssignedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("Minutes/{minuteId:long}/attendees")]
    public async Task<IActionResult> AddToMinute(
        long minuteId,
        [FromBody] AddAttendeeDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _application.AddToMinuteAsync(
                minuteId,
                request.AttendeeId,
                cancellationToken);

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (AttendeeAlreadyAssignedException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
