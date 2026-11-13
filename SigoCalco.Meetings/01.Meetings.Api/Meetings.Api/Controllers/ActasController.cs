using Meetings.Api.Responses;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/actas")]
public sealed class ActasController(IActaApplication application) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedActaDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateActaDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateAsync(request, idempotencyKey, ct),
            "Acta creada. Consulte integration para conocer el estado externo; una simulación no envía invitaciones ni confirma reservas."));
}
