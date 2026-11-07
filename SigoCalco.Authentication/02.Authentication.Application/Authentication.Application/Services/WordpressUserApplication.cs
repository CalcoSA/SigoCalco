using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Dtos;

namespace SigoCalco.Authentication.Application.Services
{
    public class WordpressUserApplication : IWordpressUserApplication
    {
        private readonly IWordpressUserRepository _wordpressUserRepository;

        public WordpressUserApplication(IWordpressUserRepository wordpressUserRepository)
        {
            _wordpressUserRepository = wordpressUserRepository;
        }

        /// <summary>
        /// Método que recibe la solicitud del controlador para consultar un usuario de WordPress por UserLogin.
        /// </summary>
        /// <param name="userLogin">Type: string - UserLogin del usuario en WordPress</param>
        /// <returns>Type: WordpressUserDto - Usuario encontrado en WordPress</returns>
        public async Task<WordpressUserDto?> GetByLogin(string userLogin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userLogin))
                    return null;

                return await _wordpressUserRepository.GetByLogin(userLogin.Trim());
            }
            catch (Exception ex)
            {
                Exception exception = new("Failed" + ex.InnerException + "\n" + ex.Message);
                throw exception;
            }
        }
    }
}