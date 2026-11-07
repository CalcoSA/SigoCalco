using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface IMenuOptionRepository : IRepository<Menuoption>
    {
        Task<IEnumerable<Menuoption>> GetByRole(int idRole);
    }
}