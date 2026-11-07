using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;

namespace SigoCalco.Authentication.Api.Extensions
{
    public class SessionJwtBearerEvents : JwtBearerEvents
    {
        private static readonly object FailureKey = new();
        private readonly IAuthApplication _authApplication;
        private readonly ILogger<SessionJwtBearerEvents> _logger;

        public SessionJwtBearerEvents(IAuthApplication authApplication, ILogger<SessionJwtBearerEvents> logger)
        {
            _authApplication = authApplication;
            _logger = logger;
        }

        public override Task MessageReceived(MessageReceivedContext context)
        {
            if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
                context.NoResult();
            return Task.CompletedTask;
        }

        public override async Task TokenValidated(TokenValidatedContext context)
        {
            var sessionIds = context.Principal!.FindAll("sid").ToList();
            var userIds = context.Principal.FindAll("idUser").ToList();
            var subjects = context.Principal.FindAll("sub").ToList();
            if (sessionIds.Count != 1 || sessionIds[0].Value.Length != 36 ||
                !Guid.TryParseExact(sessionIds[0].Value, "D", out _))
            {
                Reject("SESSION_REQUIRED", "Inicie sesión nuevamente para obtener una sesión válida.");
                return;
            }
            if (userIds.Count != 1 || subjects.Count != 1 || subjects[0].Value != userIds[0].Value ||
                !int.TryParse(userIds[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var idUser) || idUser <= 0)
            {
                Reject("TOKEN_INVALID", "El token no identifica un usuario válido.");
                return;
            }

            try
            {
                await _authApplication.ValidateSession(sessionIds[0].Value, idUser);
            }
            catch (SessionException ex)
            {
                Reject(ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo verificar el estado de la sesión.");
                Reject("SESSION_UNAVAILABLE", "No se pudo verificar la sesión. Intente nuevamente.", 503);
            }

            void Reject(string code, string message, int status = 401)
            {
                context.HttpContext.Items[FailureKey] = new Failure(status, code, message);
                context.Fail(code);
            }
        }

        public override Task AuthenticationFailed(AuthenticationFailedContext context)
        {
            var expired = context.Exception is SecurityTokenExpiredException;
            context.HttpContext.Items[FailureKey] = new Failure(401,
                expired ? "TOKEN_EXPIRED" : "TOKEN_INVALID",
                expired ? "El JWT venció. Puede intentar renovar si la sesión sigue vigente." : "El JWT no es válido.");
            return Task.CompletedTask;
        }

        public override async Task Challenge(JwtBearerChallengeContext context)
        {
            context.HandleResponse();
            var failure = context.HttpContext.Items[FailureKey] as Failure
                ?? new Failure(401, "AUTH_REQUIRED", "Se requiere un token de acceso.");
            context.Response.StatusCode = failure.Status;
            context.Response.Headers.CacheControl = "no-store";
            if (failure.Status == 401)
                context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(new ResponseApi
            {
                IsSuccess = false,
                Message = failure.Message,
                Result = new { code = failure.Code }
            });
        }

        public override async Task Forbidden(ForbiddenContext context)
        {
            context.Response.StatusCode = 403;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new ResponseApi
            {
                IsSuccess = false,
                Message = "No tiene permisos para realizar esta operación.",
                Result = new { code = "FORBIDDEN" }
            });
        }

        private sealed record Failure(int Status, string Code, string Message);
    }
}
