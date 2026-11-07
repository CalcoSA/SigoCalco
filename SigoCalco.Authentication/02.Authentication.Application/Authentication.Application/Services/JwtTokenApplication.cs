using SigoCalco.Authentication.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using SigoCalco.Authentication.Domain.Dtos;
using System.Security.Claims;
using System.Text;

namespace SigoCalco.Authentication.Application.Services
{
    public class JwtTokenApplication : IJwtTokenApplication
    {
        private readonly IConfiguration _configuration;

        public JwtTokenApplication(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Genera un token JWT para un usuario autenticado.
        /// </summary>
        /// <param name="user">Type: UserDto - Usuario autenticado</param>
        /// <returns>Type: string Token y DateTime ExpiresAt</returns>
        public (string Token, DateTime ExpiresAt) GenerateToken(UserDto user, string sessionId)
        {
            if (!Guid.TryParseExact(sessionId, "D", out _))
                throw new ArgumentException("El identificador de sesión no es válido.", nameof(sessionId));

            var secret = _configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("No se encontró Jwt:Secret.");

            var issuer = _configuration["Jwt:Issuer"];
            var audience = _configuration["Jwt:Audience"];

            var expirationMinutes = int.TryParse(_configuration["Jwt:ExpirationMinutes"], out var minutes)
                ? minutes
                : 480;

            var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.IdUser.ToString()),
                new Claim("sid", sessionId),
                new Claim("idUser", user.IdUser.ToString()),
                new Claim("wordpressUserId", user.WordpressUserId.ToString()),
                new Claim("userLogin", user.UserLogin ?? string.Empty),
                new Claim("userName", user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("idRole", user.IdRole.ToString()),
                new Claim("nameRole", user.NameRole ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(issuer: issuer, audience: audience, claims: claims, expires: expiresAt, signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
