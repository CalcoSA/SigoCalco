using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IMenuOptionApplication
    {
        Task<IEnumerable<MenuOptionDto>> GetAll();
        Task<IEnumerable<MenuOptionDto>> GetByRole(int idRole);
    }
}