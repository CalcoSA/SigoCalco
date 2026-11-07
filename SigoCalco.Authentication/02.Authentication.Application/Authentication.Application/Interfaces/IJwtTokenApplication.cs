using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IJwtTokenApplication
    {
        (string Token, DateTime ExpiresAt) GenerateToken(UserDto user, string sessionId);
    }
}
