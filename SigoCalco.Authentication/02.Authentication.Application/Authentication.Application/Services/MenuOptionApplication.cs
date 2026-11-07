using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using AutoMapper;

namespace SigoCalco.Authentication.Application.Services
{
    public class MenuOptionApplication : IMenuOptionApplication
    {
        private readonly IMenuOptionRepository _menuOptionRepository;
        private readonly IMapper _mapper;

        public MenuOptionApplication(IMenuOptionRepository menuOptionRepository, IMapper mapper)
        {
            _menuOptionRepository = menuOptionRepository;
            _mapper = mapper;
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para obtener todos los registros de la tabla MenuOption
        /// </summary>
        /// <returns>Type: IEnumerable - Lista con la información solicitada de la tabla MenuOption</returns>
        public async Task<IEnumerable<MenuOptionDto>> GetAll()
        {
            try
            {
                var menuOption = await _menuOptionRepository.GetAll();
                var responseMenuOption = _mapper.Map<IEnumerable<MenuOptionDto>>(menuOption);
                return responseMenuOption;
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para obtener un MenuOptionDto por idRole
        /// </summary>
        /// <param name="idRole">Type: int - Identificador del idRole para buscar</param>
        /// <returns>Type: MenuOptionDto - entidad con la información solicitada</returns>
        public async Task<IEnumerable<MenuOptionDto>> GetByRole(int idRole)
        {
            try
            {
                var menuOption = await _menuOptionRepository.GetByRole(idRole);
                var responseMenuOption = _mapper.Map<IEnumerable<MenuOptionDto>>(menuOption);
                return responseMenuOption;
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }
    }
}