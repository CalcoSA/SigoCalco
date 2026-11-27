using Meetings.Application.Dtos;
using Meetings.Api.Responses;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/templates")]
public sealed class TemplatesController(ITemplateApplication application) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Crear plantilla con nombre globalmente único")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<CreatedTemplateDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateDto request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateAsync(request, ct), "Plantilla creada correctamente."));

    [HttpPost("{templateId:int}/versions")]
    [EndpointDescription("Crea una versión inmutable. TEXTO admite FIXED/EDITABLE; SUBTITULO usa title, configurationJson:null e isRequired:false.")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVersion(int templateId, [FromBody] CreateTemplateVersionDto request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateVersionAsync(templateId, request, ct), "Versión creada correctamente."));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedTemplatesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] TemplateQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetAsync(query, ct), "Plantillas consultadas correctamente."));

    [HttpPost("with-version")]
    [EndpointSummary("Crear plantilla única con primera versión y secciones, de manera atómica")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<CreatedTemplateWithVersionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateWithVersion([FromBody] CreateTemplateWithVersionDto request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateWithVersionAsync(request, ct), "Plantilla y primera versión creadas correctamente."));

    [HttpGet("{templateId:int}/versions")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Versions(int templateId, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetVersionsAsync(templateId, ct), "Versiones consultadas correctamente."));

    [HttpPut("{templateId:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<TemplateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int templateId, [FromBody] UpdateTemplateDto request, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.UpdateAsync(templateId, request, ct), "Metadatos actualizados correctamente."));
}

