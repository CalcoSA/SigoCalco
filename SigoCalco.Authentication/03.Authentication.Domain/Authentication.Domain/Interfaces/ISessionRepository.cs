using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface ISessionRepository
    {
        Task Create(AuthenticationSession session);
        Task<AuthenticationSession> Validate(string sessionId, int idUser);
        Task<AuthenticationSession> RegisterActivity(string sessionId, int idUser);
        Task<AuthenticationSession> RotateRefreshToken(string sessionId, string currentHash, string newHash);
        Task Revoke(string sessionId, int idUser);
    }
}
