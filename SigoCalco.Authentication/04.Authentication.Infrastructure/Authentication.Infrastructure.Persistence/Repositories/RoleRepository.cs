using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using SigoCalco.Authentication.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Exceptions;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class RoleRepository : Repository<Role>, IRoleRepository
    {
        private readonly SigoCalcoContext _context;
        public RoleRepository(SigoCalcoContext context) : base(context)
        {
            _context = context;
        }

        /// <summary>
        /// Consulta un rol por IdRole incluyendo sus opciones de menú asociadas.
        /// </summary>
        /// <param name="idRole">Type: int - Identificador del rol a consultar</param>
        /// <returns>Type: Role - Rol con sus opciones de menú asociadas</returns>
        public async Task<Role?> GetByIdRole(int idRole)
        {
            return await _context.Roles
                .AsNoTracking()
                .Include(x => x.Rolemenuoptions)
                    .ThenInclude(x => x.IdMenuOptionNavigation)
                .FirstOrDefaultAsync(x => x.IdRole == idRole);
        }

        /// <summary>
        /// Valida si existe un rol con el mismo nombre, ignorando mayúsculas y minúsculas.
        /// </summary>
        /// <param name="nameRole">Type: string - Nombre del rol a validar</param>
        /// <param name="excludeIdRole">Type: int? - IdRole a excluir cuando se actualiza un rol</param>
        /// <returns>Type: bool - True si ya existe un rol con ese nombre</returns>
        public async Task<bool> ExistsByName(string nameRole, int? excludeIdRole = null)
        {
            var normalizedName = nameRole.Trim().ToLower();

            var query = _context.Roles
                .AsNoTracking()
                .Where(x => x.NameRole.ToLower() == normalizedName);

            if (excludeIdRole.HasValue)
            {
                query = query.Where(x => x.IdRole != excludeIdRole.Value);
            }

            return await query.AnyAsync();
        }

        /// <summary>
        /// Valida que todas las opciones de menú enviadas existan.
        /// </summary>
        /// <param name="menuOptionIds">Type: IEnumerable int - Identificadores de opciones de menú</param>
        /// <returns>Type: bool - True si todas las opciones de menú existen</returns>
        public async Task<bool> MenuOptionsExist(IEnumerable<int> menuOptionIds)
        {
            var ids = menuOptionIds
                .Distinct()
                .ToList();

            if (!ids.Any())
                return false;

            var totalFound = await _context.Menuoptions
                .AsNoTracking()
                .CountAsync(x =>
                    ids.Contains(x.IdMenuOption) &&
                    x.StatusMenuOption == 1);

            return totalFound == ids.Count;
        }

        /// <summary>
        /// Crea un rol y registra sus opciones de menú asociadas.
        /// </summary>
        /// <param name="role">Type: Role - Entidad del rol a crear</param>
        /// <param name="menuOptionIds">Type: IEnumerable int - Identificadores de opciones de menú</param>
        /// <returns></returns>
        public async Task CreateRole(Role role, IEnumerable<int> menuOptionIds)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                await _context.Roles.AddAsync(role);
                await _context.SaveChangesAsync();

                var roleMenuOptions = menuOptionIds
                    .Distinct()
                    .Select(idMenuOption => new Rolemenuoption
                    {
                        IdRole = role.IdRole,
                        IdMenuOption = idMenuOption
                    })
                    .ToList();

                await _context.Rolemenuoptions.AddRangeAsync(roleMenuOptions);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Actualiza un rol y reemplaza sus opciones de menú asociadas.
        /// </summary>
        /// <param name="role">Type: Role - Entidad del rol a actualizar</param>
        /// <param name="menuOptionIds">Type: IEnumerable int - Identificadores de opciones de menú</param>
        /// <returns></returns>
        public async Task UpdateRole(Role role, IEnumerable<int> menuOptionIds)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var currentRole = await _context.Roles
                    .FirstOrDefaultAsync(x => x.IdRole == role.IdRole);

                if (currentRole is null)
                    throw new InvalidOperationException("El rol no existe.");

                currentRole.NameRole = role.NameRole;
                currentRole.StatusRole = role.StatusRole;

                var currentRoleMenuOptions = await _context.Rolemenuoptions
                    .Where(x => x.IdRole == role.IdRole)
                    .ToListAsync();

                if (currentRoleMenuOptions.Any())
                {
                    _context.Rolemenuoptions.RemoveRange(currentRoleMenuOptions);
                }

                var newRoleMenuOptions = menuOptionIds
                    .Distinct()
                    .Select(idMenuOption => new Rolemenuoption
                    {
                        IdRole = role.IdRole,
                        IdMenuOption = idMenuOption
                    })
                    .ToList();

                await _context.Rolemenuoptions.AddRangeAsync(newRoleMenuOptions);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Elimina un rol y sus relaciones con opciones de menú.
        /// </summary>
        /// <param name="idRole">Type: int - Identificador del rol a eliminar</param>
        /// <returns></returns>
        public async Task DeleteRole(int idRole)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var role = await _context.Roles
                    .FirstOrDefaultAsync(x => x.IdRole == idRole);

                if (role is null)
                    throw new InvalidOperationException("El rol no existe.");

                if (role.RoleCode == Role.PointOfSaleCode)
                    throw new BusinessRuleException("No se puede eliminar el rol funcional PDV.");

                if (await _context.Userroles.AnyAsync(x => x.IdRole == idRole))
                    throw new BusinessRuleException("No se puede eliminar un rol con usuarios asignados. Reasigne sus usuarios conservando un Centro de Soluciones válido.");

                var roleMenuOptions = await _context.Rolemenuoptions
                    .Where(x => x.IdRole == idRole)
                    .ToListAsync();

                if (roleMenuOptions.Any())
                {
                    _context.Rolemenuoptions.RemoveRange(roleMenuOptions);
                }

                _context.Roles.Remove(role);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
