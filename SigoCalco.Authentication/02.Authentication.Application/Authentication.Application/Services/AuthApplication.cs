using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Options;
using AutoMapper;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace SigoCalco.Authentication.Application.Services
{
    public class AuthApplication : IAuthApplication
    {
        private readonly IWordpressUserRepository _wordpressUserRepository;
        private readonly ISSOSignatureApplication _ssoSignatureApplication;
        private readonly IMenuOptionApplication _menuOptionApplication;
        private readonly IJwtTokenApplication _jwtTokenApplication;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ISessionRepository _sessionRepository;
        private readonly SessionOptions _sessionOptions;
        private readonly TimeProvider _timeProvider;

        public AuthApplication(IWordpressUserRepository wordpressUserRepository,
            ISSOSignatureApplication ssoSignatureApplication,
            IMenuOptionApplication menuOptionApplication,
            IJwtTokenApplication jwtTokenApplication,
            IUserRepository userRepository,
            IMapper mapper,
            ISessionRepository sessionRepository,
            IOptions<SessionOptions> sessionOptions,
            TimeProvider timeProvider)
        {
            _wordpressUserRepository = wordpressUserRepository;
            _ssoSignatureApplication = ssoSignatureApplication;
            _menuOptionApplication = menuOptionApplication;
            _jwtTokenApplication = jwtTokenApplication;
            _userRepository = userRepository;
            _mapper = mapper;
            _sessionRepository = sessionRepository;
            _sessionOptions = sessionOptions.Value;
            _timeProvider = timeProvider;
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para autenticar un usuario con credenciales de WordPress y valida autorización local.
        /// </summary>
        /// <param name="loginData">Type: LoginDto - Usuario y contraseña</param>
        /// <returns>Type: AuthUserDto - Información del usuario autentificado</returns>
        public async Task<AuthUserDto?> Login(LoginDto loginData)
        {
            try
            {
                if (loginData is null)
                    return null;

                if (string.IsNullOrWhiteSpace(loginData.Username) || string.IsNullOrWhiteSpace(loginData.Password))
                    return null;

                var username = loginData.Username.Trim();

                var wordpressUser = await _wordpressUserRepository.GetByLogin(username);

                if (wordpressUser is null)
                    return null;

                if (string.IsNullOrWhiteSpace(wordpressUser.WordpressUserPass))
                    return null;

                if (string.IsNullOrWhiteSpace(wordpressUser.WordpressUserLogin))
                    return null;

                var passwordIsValid = _wordpressUserRepository.Verify(loginData.Password, wordpressUser.WordpressUserPass);

                if (!passwordIsValid)
                    return null;

                return await Authorize(wordpressUser.WordpressUserLogin);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para autenticar el acceso desde intranet usando userLogin, ts y sig.
        /// </summary>
        /// <param name="accessData">Type: IntranetAccessDto - Parámetros SSO de intranet</param>
        /// <returns>Type: AuthUserDto - Información del usuario autentificado</returns>
        public async Task<AuthUserDto?> IntranetAccess(IntranetAccessDto accessData)
        {
            try
            {
                if (accessData is null)
                    return null;

                if (string.IsNullOrWhiteSpace(accessData.UserLogin) || string.IsNullOrWhiteSpace(accessData.Sig))
                    return null;

                var userLogin = accessData.UserLogin.Trim();
                var sig = accessData.Sig.Trim();

                var signatureIsValid = _ssoSignatureApplication.IsValid(userLogin, accessData.Ts, sig);

                if (!signatureIsValid)
                    return null;

                var wordpressUser = await _wordpressUserRepository.GetByLogin(userLogin);

                if (wordpressUser is null)
                    return null;

                if (string.IsNullOrWhiteSpace(wordpressUser.WordpressUserLogin))
                    return null;

                return await Authorize(wordpressUser.WordpressUserLogin);
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Método privado que valida si el usuario autenticado en WordPress existe y está autorizado en el sistema local.
        /// </summary>
        /// <param name="userLogin">Type: string - UserLogin del usuario autenticado en WordPress</param>
        /// <returns>Type: AuthUserDto - Información del usuario autentificado</returns>
        private async Task<AuthUserDto?> Authorize(string userLogin)
        {
            var localUser = await _userRepository.GetUserByLogin(userLogin);
            var (user, menuOptions) = await GetAuthorizedUser(localUser);

            var sessionId = Guid.NewGuid().ToString("D");
            var refreshToken = CreateRefreshToken(sessionId);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var session = new AuthenticationSession
            {
                IdSession = sessionId,
                IdUser = user.IdUser,
                CreatedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(_sessionOptions.IdleTimeoutMinutes),
                RefreshTokenHash = HashRefreshToken(refreshToken)
            };
            var response = CreateResponse(user, menuOptions, session, refreshToken);
            await _sessionRepository.Create(session);
            return response;
        }

        public Task<AuthenticationSession> ValidateSession(string sessionId, int idUser)
            => _sessionRepository.Validate(sessionId, idUser);

        public Task<AuthenticationSession> RegisterActivity(string sessionId, int idUser)
            => _sessionRepository.RegisterActivity(sessionId, idUser);

        public Task Logout(string sessionId, int idUser)
            => _sessionRepository.Revoke(sessionId, idUser);

        public async Task<AuthUserDto> Refresh(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken) || refreshToken.Length != 80 ||
                refreshToken[36] != '.' || !Guid.TryParseExact(refreshToken[..36], "D", out _))
                throw new SessionException("REFRESH_INVALID", "El token de renovación no es válido.");

            var sessionId = refreshToken[..36];
            var nextRefreshToken = CreateRefreshToken(sessionId);
            var session = await _sessionRepository.RotateRefreshToken(
                sessionId, HashRefreshToken(refreshToken), HashRefreshToken(nextRefreshToken));
            var localUser = await _userRepository.GetUserById(session.IdUser);
            var (user, menuOptions) = await GetAuthorizedUser(localUser);
            return CreateResponse(user, menuOptions, session, nextRefreshToken);
        }

        private async Task<(UserDto User, List<MenuOptionDto> MenuOptions)> GetAuthorizedUser(User? localUser)
        {

            if (localUser is null)
                throw new UnauthorizedAccessException("El usuario no tiene permisos asignados en el aplicativo.");

            if (!localUser.StatusUser)
                throw new UnauthorizedAccessException("El usuario está inactivo en el aplicativo.");

            if (!localUser.Userroles.Any() || localUser.Userroles.Any(x => x.SolutionCenterId <= 0))
                throw new UnauthorizedAccessException("El usuario debe tener un Centro de Soluciones válido.");

            var user = _mapper.Map<UserDto>(localUser);

            var menuOptions = (await _menuOptionApplication.GetByRole(user.IdRole)).ToList();

            if (!menuOptions.Any())
                throw new UnauthorizedAccessException("El usuario no tiene permisos asignados en el aplicativo.");

            return (user, menuOptions);
        }

        private AuthUserDto CreateResponse(UserDto user, List<MenuOptionDto> menuOptions,
            AuthenticationSession session, string refreshToken)
        {
            var tokenData = _jwtTokenApplication.GenerateToken(user, session.IdSession);

            return new AuthUserDto
            {
                TokenType = "Bearer",
                AccessToken = tokenData.Token,
                ExpiresAt = tokenData.ExpiresAt,
                RefreshToken = refreshToken,
                SessionExpiresAt = session.ExpiresAt,
                User = user,
                MenuOptions = menuOptions
            };
        }

        private static string CreateRefreshToken(string sessionId)
            => $"{sessionId}.{Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32))}";

        private static string HashRefreshToken(string refreshToken)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
