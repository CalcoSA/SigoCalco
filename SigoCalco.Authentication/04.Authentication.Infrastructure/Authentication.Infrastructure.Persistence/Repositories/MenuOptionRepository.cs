using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using SigoCalco.Authentication.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class MenuOptionRepository : Repository<Menuoption>, IMenuOptionRepository
    {
        private readonly SigoCalcoContext _context;
        public MenuOptionRepository(SigoCalcoContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Consulta que obtine las opciones de menú por un IdRole
        /// </summary>
        /// <param name="idRole">Identificador del registro a obtener</param>
        /// <returns>La información del registro solicitado</returns>
        public async Task<IEnumerable<Menuoption>> GetByRole(int idRole)
        {
            return await _context.Rolemenuoptions
                .AsNoTracking()
                .Where(x =>
                    x.IdRole == idRole &&
                    x.IdMenuOptionNavigation.StatusMenuOption == 1)
                .Select(x => x.IdMenuOptionNavigation)
                .OrderBy(x => x.OrderMenuOption)
                .ToListAsync();
        }
    }
}