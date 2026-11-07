using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using AutoMapper;
using SigoCalco.Authentication.Domain.Exceptions;

namespace SigoCalco.Authentication.Application.Services
{
    public class RoleApplication : IRoleApplication
    {
        private readonly IRoleRepository _roleRepository;
        private readonly IMapper _mapper;

        public RoleApplication(IRoleRepository roleRepository, IMapper mapper)
        {
            _roleRepository = roleRepository;
            _mapper = mapper;
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para obtener todos los registros de la tabla Role
        /// </summary>
        /// <returns>Type: IEnumerable - Lista con la información solicitada de la tabla Role</returns>
        public async Task<IEnumerable<RoleDto>> GetAll()
        {
            try
            {
                var role = await _roleRepository.GetAll();
                var responseRole = _mapper.Map<IEnumerable<RoleDto>>(role);
                return responseRole;
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para obtener un Role por IdRole.
        /// </summary>
        /// <param name="idRole">Type: int - Identificador del IdRole a buscar</param>
        /// <returns>Type: RoleDetailDto - Rol con sus opciones de menú asociadas</returns>
        public async Task<RoleDetailDto> GetById(int idRole)
        {
            try
            {
                var role = await _roleRepository.GetByIdRole(idRole);

                if (role is null)
                    return null!;

                var responseRole = _mapper.Map<RoleDetailDto>(role);
                return responseRole;
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para crear un rol y sus opciones de menú asociadas.
        /// </summary>
        /// <param name="entity">Type: CreateRoleDto - Información del rol a crear</param>
        /// <returns>Resultado de la operación y descripción de auditoría si fue exitosa</returns>
        public async Task<AuditResultDto> Create(CreateRoleDto entity)
        {
            try
            {
                if (entity is null || string.IsNullOrWhiteSpace(entity.NameRole))
                    return new AuditResultDto();

                var roleNameExists = await _roleRepository.ExistsByName(entity.NameRole);

                if (roleNameExists)
                    return new AuditResultDto();

                var menuOptionsExist = await _roleRepository.MenuOptionsExist(entity.MenuOptionIds);

                if (!menuOptionsExist)
                    return new AuditResultDto();

                var role = new Role
                {
                    NameRole = entity.NameRole.Trim(),
                    StatusRole = entity.StatusRole
                };

                await _roleRepository.CreateRole(role, entity.MenuOptionIds);

                return new AuditResultDto
                {
                    IsSuccess = true,
                    Description = $"Se creó el rol {role.NameRole} con sus opciones de menú."
                };
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para actualizar un rol y sus opciones de menú asociadas.
        /// </summary>
        /// <param name="entity">Type: UpdateRoleDto - Información del rol a actualizar</param>
        /// <returns>Resultado de la operación y descripción de auditoría si fue exitosa</returns>
        public async Task<AuditResultDto> Update(UpdateRoleDto entity)
        {
            try
            {
                if (entity is null || entity.IdRole <= 0 || string.IsNullOrWhiteSpace(entity.NameRole))
                    return new AuditResultDto();

                var currentRole = await _roleRepository.GetById(entity.IdRole);

                if (currentRole is null)
                    return new AuditResultDto();

                var roleNameExists = await _roleRepository.ExistsByName(entity.NameRole, entity.IdRole);

                if (roleNameExists)
                    return new AuditResultDto();

                var menuOptionsExist = await _roleRepository.MenuOptionsExist(entity.MenuOptionIds);

                if (!menuOptionsExist)
                    return new AuditResultDto();

                var role = new Role
                {
                    IdRole = entity.IdRole,
                    NameRole = entity.NameRole.Trim(),
                    StatusRole = entity.StatusRole
                };

                await _roleRepository.UpdateRole(role, entity.MenuOptionIds);

                return new AuditResultDto
                {
                    IsSuccess = true,
                    Description = $"Se actualizó el rol {role.NameRole}: estado {(role.StatusRole == 1 ? "activo" : "inactivo")} y opciones de menú."
                };
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para eliminar un rol por IdRole.
        /// </summary>
        /// <param name="idRole">Type: int - Identificador del rol a eliminar</param>
        /// <returns>Resultado de la operación y descripción de auditoría si fue exitosa</returns>
        public async Task<AuditResultDto> Delete(int idRole)
        {
            try
            {
                if (idRole <= 0)
                    return new AuditResultDto();

                var role = await _roleRepository.GetById(idRole);

                if (role is null)
                    return new AuditResultDto();

                var roleName = role.NameRole;
                if (role.RoleCode == Role.PointOfSaleCode)
                    throw new BusinessRuleException("No se puede eliminar el rol funcional PDV.");
                await _roleRepository.DeleteRole(idRole);

                return new AuditResultDto
                {
                    IsSuccess = true,
                    Description = $"Se eliminó el rol {roleName} y sus opciones de menú asociadas."
                };
            }
            catch (BusinessRuleException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }
    }
}
