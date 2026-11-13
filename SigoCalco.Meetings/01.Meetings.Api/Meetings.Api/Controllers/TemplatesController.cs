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
    [ProducesResponseType(typeof(ApiResponse<CreatedTemplateDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTemplateDto request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateAsync(request, ct), "Plantilla creada correctamente."));

    [HttpPost("{templateId:int}/versions")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateVersion(int templateId, [FromBody] CreateTemplateVersionDto request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, ApiResponse.Success(await application.CreateVersionAsync(templateId, request, ct), "Versión creada correctamente."));

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedTemplatesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] TemplateQuery query, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetAsync(query, ct)));
}

