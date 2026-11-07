using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SigoCalco.Authentication.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class LogController : ControllerBase
    {
        private readonly ILogApplication _logApplication;
        private readonly ILogger<LogController> _logger;

        public LogController(ILogApplication logApplication, ILogger<LogController> logger)
        {
            _logApplication = logApplication;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetPaged(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int take = 10)
        {
            try
            {
                if (page <= 0 || take <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "Page y Take deben ser mayores a cero.",
                        Result = new { }
                    });
                }

                if (from.HasValue && to.HasValue && from.Value.Date > to.Value.Date)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La fecha desde no puede ser mayor que la fecha hasta.",
                        Result = new { }
                    });
                }

                var logs = await _logApplication.GetPaged(from, to, page, take);

                if (logs is null)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El rango de fechas no es válido.",
                        Result = new { }
                    });
                }

                if (!logs.Items.Any())
                {
                    return Ok(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No se encontraron registros de actividad.",
                        Result = logs
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Logs consultados correctamente.",
                    Result = logs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar los logs.");

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar los logs.",
                    Result = new { }
                });
            }
        }
    }
}
