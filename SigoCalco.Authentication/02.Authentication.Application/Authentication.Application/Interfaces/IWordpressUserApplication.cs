using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IWordpressUserApplication
    {
        Task<WordpressUserDto?> GetByLogin(string userLogin);
    }
}