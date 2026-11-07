using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Domain.Interfaces
{
    public interface ISolutionCenterRepository
    {
        Task<IEnumerable<SolutionCenter>> GetAll();
    }
}
