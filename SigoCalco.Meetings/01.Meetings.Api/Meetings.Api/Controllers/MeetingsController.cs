
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Route("api/Meetings")]
[Authorize]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingApplication _meetingApplication;

    public MeetingsController(
        IMeetingApplication meetingApplication)
    {
        _meetingApplication = meetingApplication;
    }

    /// <summary>
    /// Crea una reunión junto con su acta en borrador.
    /// </summary>
    /// <param name="request">
    /// Datos necesarios para crear la reunión.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelación.
    /// </param>
    /// <returns>
    /// Identificadores y estados de la reunión y el acta.
    /// </returns>
    [HttpPost]
    public async Task<ActionResult<CreateMeetingResponseDto>> Create(
        [FromBody] CreateMeetingDto request,
        CancellationToken cancellationToken)
    {
        // El ID debe provenir del JWT autenticado.
        var userIdClaim = User.FindFirst("idUser")?.Value;

        if (!long.TryParse(userIdClaim, out var userId) ||
            userId <= 0)
        {
            return Unauthorized(new
            {
                message = "No se encontró un usuario válido en el token."
            });
        }

        try
        {
            var response = await _meetingApplication.CreateAsync(
                request,
                userId,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    /// <summary>
    /// Consulta una reunión por su identificador,
    /// incluyendo su acta asociada.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<GetMeetingResponseDto>> GetById(
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

        var meeting = await _meetingApplication.GetByIdAsync(
            id,
            cancellationToken);

        if (meeting is null)
        {
            return NotFound(new
            {
                message = "No se encontró la reunión solicitada."
            });
        }

        return Ok(meeting);
    }

}
