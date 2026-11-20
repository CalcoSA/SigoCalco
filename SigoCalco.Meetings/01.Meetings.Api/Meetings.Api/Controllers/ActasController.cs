using Meetings.Api.Responses;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/actas")]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
public sealed class ActasController(IActaApplication application) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Mis actas: solo actas propias con ACTAS_GESTION")]
    [ProducesResponseType(typeof(ApiResponse<PagedActasDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] ActaQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetAsync(query, ct), "Actas consultadas correctamente."));

    [HttpGet("available-templates")]
    [EndpointSummary("Plantillas visibles con su última versión para crear actas")]
    [ProducesResponseType(typeof(ApiResponse<PagedAvailableTemplatesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableTemplates([FromQuery] TemplateQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetAvailableTemplatesAsync(query, ct), "Plantillas disponibles consultadas correctamente."));

    [HttpGet("{actaId:int}")]
    [ProducesResponseType(typeof(ApiResponse<ActaDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Detail(int actaId, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetByIdAsync(actaId, ct), "Acta consultada correctamente."));

    [HttpPut("{actaId:int}")]
    [EndpointSummary("Editar título, fecha y contenido de un acta propia en BORRADOR")]
    [ProducesResponseType(typeof(ApiResponse<ActaDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int actaId, [FromBody] UpdateActaDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.UpdateAsync(actaId, request, ct), "Acta actualizada correctamente."));

    [HttpPut("{actaId:int}/participants")]
    [ProducesResponseType(typeof(ApiResponse<ActaDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Participants(int actaId, [FromBody] UpdateActaParticipantsDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.UpdateParticipantsAsync(actaId, request, ct), "Participantes actualizados correctamente."));

    [HttpPut("{actaId:int}/participants/{participantId:int}/attendance")]
    [ProducesResponseType(typeof(ApiResponse<ActaDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Attendance(int actaId, int participantId, [FromBody] UpdateAttendanceDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.UpdateAttendanceAsync(actaId, participantId, request, ct), "Asistencia actualizada correctamente."));

    [HttpPatch("{actaId:int}/status")]
    [EndpointSummary("Publicar o anular un acta propia; ANULADA es terminal")]
    [ProducesResponseType(typeof(ApiResponse<ActaDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(int actaId, [FromBody] UpdateActaStatusDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.ChangeStatusAsync(actaId, request, ct), "Estado actualizado correctamente."));

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedActaDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateActaDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateAsync(request, idempotencyKey, ct),
            "Acta creada. Consulte integration y notifications: una simulación no envía correos ni confirma reservas; ENVIADO indica aceptación SMTP, no entrega final."));
}
