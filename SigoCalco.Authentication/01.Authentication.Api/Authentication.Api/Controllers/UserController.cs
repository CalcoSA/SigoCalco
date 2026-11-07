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
    public class UserController : ControllerBase
    {
        private readonly IValidator<CreateUserDto> _createUserValidator;
        private readonly IValidator<UpdateUserDto> _updateUserValidator;
        private readonly IUserApplication _userApplication;
        private readonly ILogApplication _logApplication;
        private readonly ILogger<UserController> _logger;

        public UserController(IValidator<CreateUserDto> createUserValidator,
            IValidator<UpdateUserDto> updateUserValidator,
            IUserApplication userApplication,
            ILogApplication logApplication,
            ILogger<UserController> logger)
        {
            _createUserValidator = createUserValidator;
            _updateUserValidator = updateUserValidator;
            _userApplication = userApplication;
            _logApplication = logApplication;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int take = 10, [FromQuery] string? search = null)
        {
            try
            {
                if (page <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La página debe ser mayor a cero.",
                        Result = new { }
                    });
                }

                if (take <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La cantidad de registros por página debe ser mayor a cero.",
                        Result = new { }
                    });
                }

                var users = await _userApplication.GetAll(page, take, search);

                if (!users.Items.Any())
                {
                    return Ok(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No hay usuarios registrados para los filtros enviados.",
                        Result = users
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Usuarios consultados correctamente.",
                    Result = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar usuarios. Page: {Page}, Take: {Take}, Search: {Search}", page, take, search);
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar los usuarios.",
                    Result = new { }
                });
            }
        }

        [HttpGet("{idUser:int}")]
        public async Task<IActionResult> GetById(int idUser)
        {
            try
            {
                if (idUser <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El IdUser debe ser mayor a cero.",
                        Result = new { }
                    });
                }

                var user = await _userApplication.GetById(idUser);

                if (user is null)
                {
                    return NotFound(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El usuario no existe.",
                        Result = new { }
                    });
                }

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Usuario consultado correctamente.",
                    Result = user
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el usuario con IdUser {IdUser}.", idUser);
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al consultar el usuario.",
                    Result = new { }
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserDto request)
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

                var validation = await _createUserValidator.ValidateAsync(request);

                if (!validation.IsValid)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La solicitud no es válida.",
                        Result = validation.Errors.Select(x => x.ErrorMessage)
                    });
                }

                var created = await _userApplication.Create(request);

                if (!created.IsSuccess)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No se pudo crear el usuario. Verifique que el rol exista y que el usuario no esté repetido.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Crear",
                    Module = "ConfiguracionUsuarios",
                    Description = created.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Usuario creado correctamente.",
                    Result = (object?)created.User ?? new { }
                });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new ResponseApi { IsSuccess = false, Message = ex.Message, Result = new { } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear un usuario.");
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al crear el usuario.",
                    Result = new { }
                });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Update([FromBody] UpdateUserDto request)
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

                var validation = await _updateUserValidator.ValidateAsync(request);

                if (!validation.IsValid)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "La solicitud no es válida.",
                        Result = validation.Errors.Select(x => x.ErrorMessage)
                    });
                }

                var updated = await _userApplication.Update(request);

                if (!updated.IsSuccess)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "No se pudo actualizar el usuario. Verifique que el usuario exista y que el rol sea válido.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Actualizar",
                    Module = "ConfiguracionUsuarios",
                    Description = updated.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Usuario actualizado correctamente.",
                    Result = (object?)updated.User ?? new { }
                });
            }
            catch (BusinessRuleException ex)
            {
                return BadRequest(new ResponseApi { IsSuccess = false, Message = ex.Message, Result = new { } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el usuario con IdUser {IdUser}.", request?.IdUser);
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al actualizar el usuario.",
                    Result = new { }
                });
            }
        }

        [HttpDelete("{idUser:int}")]
        public async Task<IActionResult> Delete(int idUser)
        {
            var userName = User.GetUserLogin();

            try
            {
                if (idUser <= 0)
                {
                    return BadRequest(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El IdUser debe ser mayor a cero.",
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

                var deleted = await _userApplication.Delete(idUser);

                if (!deleted.IsSuccess)
                {
                    return NotFound(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "El usuario no existe o no se pudo eliminar.",
                        Result = new { }
                    });
                }

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "Eliminar",
                    Module = "ConfiguracionUsuarios",
                    Description = deleted.Description,
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Usuario eliminado correctamente.",
                    Result = new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el usuario con IdUser {IdUser}.", idUser);
                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al eliminar el usuario.",
                    Result = new { }
                });
            }
        }
    }
}
