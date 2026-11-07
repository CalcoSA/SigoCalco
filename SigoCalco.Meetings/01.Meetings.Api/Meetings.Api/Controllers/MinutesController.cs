
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Route("api/Minutes")]
[Authorize]
public class MinutesController : ControllerBase
{
    private readonly IMinuteApplication _minuteApplication;

    public MinutesController(
        IMinuteApplication minuteApplication)
    {
        _minuteApplication = minuteApplication;
    }

    [HttpPost]
    public async Task<ActionResult<MinuteResponseDto>> Create(
        [FromBody] CreateMinuteDto request,
        CancellationToken cancellationToken)
    {
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
            var response = await _minuteApplication.CreateAsync(
                request, userId, cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = response.MinuteId },
                response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<MinuteResponseDto>> GetById(
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

        var minute = await _minuteApplication.GetByIdAsync(
            id, cancellationToken);

        if (minute is null)
        {
            return NotFound(new
            {
                message = "No se encontró el acta solicitada."
            });
        }

        return Ok(minute);
    }
}
