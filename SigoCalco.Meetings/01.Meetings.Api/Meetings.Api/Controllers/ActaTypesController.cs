using Meetings.Application.Interfaces;
using Meetings.Api.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Meetings.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/acta-types")]
public sealed class ActaTypesController(ITemplateApplication application) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<Meetings.Application.Dtos.ActaTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(ApiResponse.Success(await application.GetTypesAsync(ct)));
}

