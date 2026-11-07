using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Interfaces
{
    public interface ISolutionCenterApplication
    {
        Task<IEnumerable<SolutionCenterDto>> GetAll();
    }
}
