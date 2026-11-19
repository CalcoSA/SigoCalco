using Meetings.Api.Responses;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/template-versions")]
public sealed class TemplateVersionsController(ITemplateApplication application) : ControllerBase
{
    [HttpGet("{templateVersionId:int}")]
    [ProducesResponseType(typeof(ApiResponse<TemplateVersionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int templateVersionId, CancellationToken ct) =>
        Ok(ApiResponse.Success(await application.GetVersionAsync(templateVersionId, ct), "Versión consultada correctamente."));
}
