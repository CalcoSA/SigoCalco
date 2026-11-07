using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Api.Extensions;
using SigoCalco.Authentication.Domain.Responses;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SigoCalco.Authentication.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RoleController : ControllerBase
    {
        private readonly IValidator<CreateRoleDto> _createRoleValidator;
        private readonly IValidator<UpdateRoleDto> _updateRoleValidator;
        private readonly IRoleApplication _roleApplication;
        private readonly ILogApplication _logApplication;
        private readonly ILogger<RoleController> _logger;

        public RoleController(IValidator<CreateRoleDto> createRoleValidator,
            IValidator<UpdateRoleDto> updateRoleValidator,
            IRoleApplication roleApplication,
            ILogApplication logApplication,
            ILogger<RoleController> logger)
        {
            _createRoleValidator = createRoleValidator;
            _updateRoleValidator = updateRoleValidator;
            _roleApplication = roleApplication;
            _logApplication = logApplication;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var roles = (await _roleApplication.GetAll()).ToList();

                if (!roles.Any())
                {
                    return Ok(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No hay roles registrados.",
                        Result = roles
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Roles consultados correctamente.",
                    Result = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar todos los roles.");

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar los roles.",
                    Result = new { }
                });
            }
        }

        [HttpGet("{idRole:int}")]
        public async Task<IActionResult> GetById(int idRole)
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

                var role = await _roleApplication.GetById(idRole);

                if (role is null)
                {
                    return NotFound(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El rol no existe.",
                        Result = new { }
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Rol consultado correctamente.",
                    Result = role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el rol con IdRole {IdRole}.", idRole);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar el rol.",
                    Result = new { }
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateRoleDto request)
        {
            var userName = User.GetUserLogin();

            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El token no contiene un userLogin válido.",
                        Result = new { }
                    });
                }

                var validation = await _createRoleValidator.ValidateAsync(request);

                if (!validation.IsValid)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La solicitud no es válida.",
                        Result = validation.Errors.Select(x => x.ErrorMessage)
                    });
                }

                var created = await _roleApplication.Create(request);

                if (!created.IsSuccess)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No se pudo crear el rol. Verifique que el nombre no exista y que las opciones de menú sean válidas.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Crear",
                    Module = "ConfiguracionRoles",
                    Description = created.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Rol creado correctamente.",
                    Result = new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear un rol.");

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al crear el rol.",
                    Result = new { }
                });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateRoleDto request)
        {
            var userName = User.GetUserLogin();

            try
            {
                if (string.IsNullOrWhiteSpace(userName))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El token no contiene un userLogin válido.",
                        Result = new { }
                    });
                }

                var validation = await _updateRoleValidator.ValidateAsync(request);

                if (!validation.IsValid)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La solicitud no es válida.",
                        Result = validation.Errors.Select(x => x.ErrorMessage)
                    });
                }

                var updated = await _roleApplication.Update(request);

                if (!updated.IsSuccess)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No se pudo actualizar el rol. Verifique que exista, que el nombre no esté repetido y que las opciones de menú sean válidas.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Actualizar",
                    Module = "ConfiguracionRoles",
                    Description = updated.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Rol actualizado correctamente.",
                    Result = new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el rol con IdRole {IdRole}.", request?.IdRole);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al actualizar el rol.",
                    Result = new { }
                });
            }
        }

        [HttpDelete("{idRole:int}")]
        public async Task<IActionResult> Delete(int idRole)
        {
            var userName = User.GetUserLogin();

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

                if (string.IsNullOrWhiteSpace(userName))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El token no contiene un userLogin válido.",
                        Result = new { }
                    });
                }

                var deleted = await _roleApplication.Delete(idRole);

                if (!deleted.IsSuccess)
                {
                    return NotFound(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El rol no existe o no se pudo eliminar.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Eliminar",
                    Module = "ConfiguracionRoles",
                    Description = deleted.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Rol eliminado correctamente.",
                    Result = new { }
                });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new ResponseApi { IsSuccess = false, Message = ex.Message, Result = new { } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el rol con IdRole {IdRole}.", idRole);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al eliminar el rol.",
                    Result = new { }
                });
            }
        }
    }
}
