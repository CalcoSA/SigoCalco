using SigoCalco.Authentication.Domain.Dtos;

using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IAuthApplication
    {
        Task<AuthUserDto?> Login(LoginDto loginData);
        Task<AuthUserDto?> IntranetAccess(IntranetAccessDto accessData);
        Task<AuthenticationSession> ValidateSession(string sessionId, int idUser);
        Task<AuthenticationSession> RegisterActivity(string sessionId, int idUser);
        Task<AuthUserDto> Refresh(string refreshToken);
        Task Logout(string sessionId, int idUser);
    }
}
