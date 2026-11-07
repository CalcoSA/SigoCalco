using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface IRoleRepository : IRepository<Role>
    {
        Task<Role?> GetByIdRole(int idRole);
        Task<bool> ExistsByName(string nameRole, int? excludeIdRole = null);
        Task<bool> MenuOptionsExist(IEnumerable<int> menuOptionIds);
        Task CreateRole(Role role, IEnumerable<int> menuOptionIds);
        Task UpdateRole(Role role, IEnumerable<int> menuOptionIds);
        Task DeleteRole(int idRole);
    }
}