using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IRoleApplication
    {
        Task<IEnumerable<RoleDto>> GetAll();
        Task<RoleDetailDto> GetById(int idRole);
        Task<AuditResultDto> Create(CreateRoleDto entity);
        Task<AuditResultDto> Update(UpdateRoleDto entity);
        Task<AuditResultDto> Delete(int Id);
    }
}
