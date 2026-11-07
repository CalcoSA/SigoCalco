using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface ILogRepository
    {
        Task<long> CreateLog(AuthenticationLog log);

        Task<PagedDto<AuthenticationLog>> GetPaged(DateTime? from, DateTime? to, int page, int take);
    }
}
