
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Route("api/Attendees")]
[Authorize]
public class AttendeesController : ControllerBase
{
    private readonly IAttendeeApplication _attendeeApplication;

    public AttendeesController(
        IAttendeeApplication attendeeApplication)
    {
        _attendeeApplication = attendeeApplication;
    }

    [HttpPost]
    public async Task<ActionResult<AttendeeResponseDto>> Create(
        [FromBody] CreateAttendeeDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _attendeeApplication
                .CreateAsync(request, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.AttendeeId },
                result);
        }
        catch (AttendeeAlreadyExistsException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AttendeeResponseDto>> GetById(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest(new
            {
                message = "El identificador debe ser mayor que cero."
            });
        }

        var attendee = await _attendeeApplication
            .GetByIdAsync(id, cancellationToken);

        if (attendee is null)
        {
            return NotFound(new
            {
                message = "No se encontró el asistente."
            });
        }

        return Ok(attendee);
    }
}
