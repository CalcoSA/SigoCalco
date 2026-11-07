using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface IUserApplication
    {
        Task<PagedDto<UserAdministrationDto>> GetAll(int page, int take, string? search);
        Task<UserAdministrationDto> GetById(int idUser);
        Task<AuditResultDto> Create(CreateUserDto entity);
        Task<AuditResultDto> Update(UpdateUserDto entity);
        Task<AuditResultDto> Delete(int idUser);
    }
}
