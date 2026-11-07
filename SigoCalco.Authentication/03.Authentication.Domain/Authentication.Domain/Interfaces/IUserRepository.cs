using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface IUserRepository : IRepository<User>
    {
        Task<PagedDto<User>> GetAllUsers(int page, int take, string? search);
        Task<User?> GetUserById(int idUser);
        Task<User?> GetAdministrativeUserById(int idUser);
        Task<User?> GetUserByLogin(string userLogin);
        Task<bool> ExistsByUserLogin(string userLogin, int? excludeIdUser = null);
        Task<UserAssignmentResultDto> CreateUser(User user, int idRole, int? solutionCenterId);
        Task<UserAssignmentResultDto> UpdateUser(int idUser, bool? statusUser, int? idRole, int? solutionCenterId, string? email = null);
        Task DeleteUser(int idUser);
    }
}
