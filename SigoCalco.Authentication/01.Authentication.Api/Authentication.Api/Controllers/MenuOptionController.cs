using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SigoCalco.Authentication.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MenuOptionController : ControllerBase
    {
        private readonly IMenuOptionApplication _menuOptionApplication;
        private readonly ILogger<MenuOptionController> _logger;

        public MenuOptionController(IMenuOptionApplication menuOptionApplication, ILogger<MenuOptionController> logger)
        {
            _menuOptionApplication = menuOptionApplication;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var menuOptions = (await _menuOptionApplication.GetAll()).ToList();

                if (!menuOptions.Any())
                {
                    return Ok(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No hay opciones de menú registradas.",
                        Result = menuOptions
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Opciones de menú consultadas correctamente.",
                    Result = menuOptions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar todas las opciones de menú.");

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar las opciones de menú.",
                    Result = new { }
                });
            }
        }

        [HttpGet("role/{idRole:int}")]
        public async Task<IActionResult> GetByRole(int idRole)
        {
            try
            {
                if (idRole <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El IdRole debe ser mayor a cero.",
                        Result = new { }
                    });
                }

                var menuOptions = (await _menuOptionApplication.GetByRole(idRole)).ToList();

                if (!menuOptions.Any())
                {
                    return Ok(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El rol no tiene opciones de menú asignadas.",
                        Result = menuOptions
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Opciones de menú por rol consultadas correctamente.",
                    Result = menuOptions
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al consultar opciones de menú por IdRole {IdRole}.",
                    idRole);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar las opciones de menú por rol.",
                    Result = new { }
                });
            }
        }
    }
}