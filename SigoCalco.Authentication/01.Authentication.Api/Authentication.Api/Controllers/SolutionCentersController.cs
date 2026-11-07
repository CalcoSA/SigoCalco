using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SigoCalco.Authentication.Api.Controllers
{
    [ApiController]
    [Route("api/Authentication/SolutionCenters")]
    [Authorize]
    public class SolutionCentersController : ControllerBase
    {
        private readonly ISolutionCenterApplication _application;
        private readonly ILogger<SolutionCentersController> _logger;

        public SolutionCentersController(ISolutionCenterApplication application,
            ILogger<SolutionCentersController> logger)
        {
            _application = application;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var centers = (await _application.GetAll()).ToList();
                return Ok(new ResponseApi
                {
                    IsSuccess = centers.Count > 0,
                    Message = centers.Count > 0
                        ? "Puntos de venta consultados correctamente."
                        : "No hay puntos de venta registrados.",
                    Result = centers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar los puntos de venta.");
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar los puntos de venta.",
                    Result = new { }
                });
            }
        }
    }
}
