using SigoCalco.Authentication.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace SigoCalco.Authentication.Application.Services
{
    public class SSOSignatureApplication : ISSOSignatureApplication
    {
        private readonly IConfiguration _configuration;
        public SSOSignatureApplication(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Valida la firma HMAC SHA256 enviada por la intranet.
        /// </summary>
        /// <param name="userLogin">Type: string - Usuario autenticado en WordPress</param>
        /// <param name="ts">Type: long - Timestamp Unix</param>
        /// <param name="sig">Type: string - Firma HMAC recibida</param>
        /// <returns>Type: bool - True si la firma es válida y vigente</returns>
        public bool IsValid(string userLogin, long ts, string sig)
        {
            if (string.IsNullOrWhiteSpace(userLogin) || string.IsNullOrWhiteSpace(sig))
                return false;

            var secret = _configuration["Sso:Secret"];

            if (string.IsNullOrWhiteSpace(secret))
                return false;

            var expirationSeconds = int.TryParse(_configuration["Sso:ExpirationSeconds"], out var seconds)
                ? seconds
                : 300;

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (Math.Abs(now - ts) > expirationSeconds)
                return false;

            var payload = $"{userLogin.Trim()}|{ts}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSig = Convert.ToHexString(hashBytes).ToLowerInvariant();

            return FixedTimeEquals(expectedSig, sig.Trim().ToLowerInvariant());
        }

        /// <summary>
        /// Método privado que compara dos cadenas en tiempo constante para evitar ataques por análisis de tiempo.
        /// </summary>
        /// <param name="a">Type: string - Primera cadena a comparar</param>
        /// <param name="b">Type: string - Segunda cadena a comparar</param>
        /// <returns>Type: bool - True si ambas cadenas son iguales; False en caso contrario</returns>
        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);

            return aBytes.Length == bBytes.Length && CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}