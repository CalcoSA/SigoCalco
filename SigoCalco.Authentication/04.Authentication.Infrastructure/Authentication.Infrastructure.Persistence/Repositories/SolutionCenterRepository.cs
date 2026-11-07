using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class SolutionCenterRepository : ISolutionCenterRepository
    {
        private readonly SigoCalcoContext _context;

        public SolutionCenterRepository(SigoCalcoContext context) => _context = context;

        public async Task<IEnumerable<SolutionCenter>> GetAll()
            => await _context.SolutionCenters.AsNoTracking()
                .OrderBy(x => x.SolutionCenterCode)
                .ThenBy(x => x.SolutionCenterId)
                .ToListAsync();
    }
}
