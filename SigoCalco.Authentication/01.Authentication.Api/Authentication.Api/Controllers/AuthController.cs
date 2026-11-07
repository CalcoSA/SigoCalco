using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Responses;
using SigoCalco.Authentication.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace SigoCalco.Authentication.Api.Controllers
{
    [ApiController]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthApplication _authApplication;
        private readonly ILogApplication _logApplication;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthApplication authApplication,
            ILogApplication logApplication,
            ILogger<AuthController> logger)
        {
            _authApplication = authApplication;
            _logApplication = logApplication;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("activity")]
        public async Task<IActionResult> Activity()
        {
            try
            {
                var session = await _authApplication.RegisterActivity(User.FindFirst("sid")!.Value,
                    int.Parse(User.FindFirst("idUser")!.Value, CultureInfo.InvariantCulture));
                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Actividad registrada.",
                    Result = new { sessionExpiresAt = session.ExpiresAt }
                });
            }
            catch (SessionException ex)
            {
                return SessionFailure(ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar actividad de la sesión.");
                return InternalFailure();
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto request)
        {
            try
            {
                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Token renovado.",
                    Result = await _authApplication.Refresh(request.RefreshToken)
                });
            }
            catch (SessionException ex)
            {
                return SessionFailure(ex);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "El usuario ya no tiene permisos para acceder al aplicativo.",
                    Result = new { code = "FORBIDDEN" }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al renovar el token de acceso.");
                return InternalFailure();
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await _authApplication.Logout(User.FindFirst("sid")!.Value,
                    int.Parse(User.FindFirst("idUser")!.Value, CultureInfo.InvariantCulture));
                return Ok(new ResponseApi { IsSuccess = true, Message = "Sesión cerrada.", Result = new { } });
            }
            catch (SessionException ex)
            {
                return SessionFailure(ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar la sesión.");
                return InternalFailure();
            }
        }

        private ObjectResult SessionFailure(SessionException exception)
        {
            var status = exception.Code == "ACTIVITY_THROTTLED" ? 429 : 401;
            if (status == 429)
                Response.Headers.RetryAfter = exception.RetryAfterSeconds!.Value.ToString(CultureInfo.InvariantCulture);
            else
                Response.Headers.WWWAuthenticate = "Bearer";
            return StatusCode(status, new ResponseApi
            {
                IsSuccess = false,
                Message = exception.Message,
                Result = new
                {
                    code = exception.Code,
                    sessionExpiresAt = exception.ExpiresAt,
                    retryAfterSeconds = exception.RetryAfterSeconds
                }
            });
        }

        private ObjectResult InternalFailure() => StatusCode(500, new ResponseApi
        {
            IsSuccess = false,
            Message = "Ocurrió un error interno al procesar la sesión.",
            Result = new { code = "INTERNAL_ERROR" }
        });

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto request)
        {
            try
            {
                var authResponse = await _authApplication.Login(request);

                if (authResponse is null)
                {
                    return Unauthorized(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "Usuario o contraseña incorrectos.",
                        Result = new { }
                    });
                }

                var userName = authResponse.User?.UserLogin;
                if (string.IsNullOrWhiteSpace(userName))
                    throw new InvalidOperationException("No se pudo identificar al usuario autenticado para registrar su acceso.");

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "IniciarSesion",
                    Module = "Autenticacion",
                    Description = "Se inició sesión correctamente.",
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Login exitoso.",
                    Result = authResponse
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ResponseApi
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Result = new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en login para usuario {Username}.", request?.Username);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al iniciar sesión.",
                    Result = new { }
                });
            }
        }

        [AllowAnonymous]
        [HttpGet("intranet-access")]
        public async Task<IActionResult> IntranetAccess([FromQuery] string userLogin, [FromQuery] long ts, [FromQuery] string sig)
        {
            try
            {
                var request = new IntranetAccessDto
                {
                    UserLogin = userLogin,
                    Ts = ts,
                    Sig = sig
                };

                var authResponse = await _authApplication.IntranetAccess(request);

                if (authResponse is null)
                {
                    return Unauthorized(new ResponseApi
                    {
                        IsSuccess = false,
                        Message = "Acceso no autorizado. La firma no es válida, expiró o el usuario no tiene permisos.",
                        Result = new { }
                    });
                }

                var userName = authResponse.User?.UserLogin;
                if (string.IsNullOrWhiteSpace(userName))
                    throw new InvalidOperationException("No se pudo identificar al usuario autenticado para registrar su acceso.");

                await _logApplication.CreateLog(new CreateLogDto
                {
                    Action = "IniciarSesion",
                    Module = "Autenticacion",
                    Description = "Se inició sesión correctamente desde intranet.",
                    UserName = userName
                });

                return Ok(new ResponseApi
                {
                    IsSuccess = true,
                    Message = "Acceso desde intranet exitoso.",
                    Result = authResponse
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ResponseApi
                {
                    IsSuccess = false,
                    Message = ex.Message,
                    Result = new { }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en acceso de intranet para usuario {UserLogin}.", userLogin);

                return StatusCode(500, new ResponseApi
                {
                    IsSuccess = false,
                    Message = "Ocurrió un error al validar el acceso desde intranet.",
                    Result = new { }
                });
            }
        }
    }
}
