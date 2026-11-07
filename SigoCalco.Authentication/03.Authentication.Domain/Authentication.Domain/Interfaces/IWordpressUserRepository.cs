using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface IWordpressUserRepository
    {
        Task<WordpressUserDto?> GetByLogin(string userLogin);
        bool Verify(string password, string wordpressHash);
    }
}