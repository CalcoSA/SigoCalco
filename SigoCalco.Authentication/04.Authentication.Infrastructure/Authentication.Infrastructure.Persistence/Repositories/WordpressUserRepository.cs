using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Data.Common;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class WordpressUserRepository : IWordpressUserRepository
    {
        private readonly string _connectionString;
        private const string Itoa64 = "./0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        public WordpressUserRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("WordPressDatabase")
                ?? throw new InvalidOperationException("No se encontró la cadena de conexión 'WordPressDatabase'.");
        }

        /// <summary>
        /// Consulta un usuario de WordPress por user_login.
        /// </summary>
        /// <param name="userLogin">Type: string - UserLogin del usuario</param>
        /// <returns>Type: WordpressUserDto - Usuario encontrado en WordPress</returns>
        internal const string UserByLoginQuery = @"
                SELECT
                    ID AS wordpressUserId,
                    user_login AS wordpressUserLogin,
                    display_name AS wordpressDisplayName,
                    user_email AS wordpressUserEmail,
                    user_pass AS wordpressUserPass
                FROM wp_users
                WHERE user_login = @username
                LIMIT 1;
            ";

        public async Task<WordpressUserDto?> GetByLogin(string userLogin)
            => await GetUser(UserByLoginQuery, userLogin);

        /// <summary>
        /// Ejecuta una consulta SQL parametrizada contra la base de datos de WordPress y construye el DTO del usuario.
        /// </summary>
        /// <param name="query">Type: string - Consulta SQL a ejecutar</param>
        /// <param name="username">Type: string - Usuario o correo a buscar</param>
        /// <returns>Type: WordpressUserDto - Usuario encontrado o null si no existe</returns>
        private async Task<WordpressUserDto?> GetUser(string query, string username)
        {
            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand(query, connection);
            command.Parameters.AddWithValue("@username", username.Trim());

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
                return null;

            return MapUser(reader);
        }

        internal static WordpressUserDto MapUser(DbDataReader reader)
        {
            return new WordpressUserDto
            {
                WordpressUserId = reader.GetFieldValue<ulong>(reader.GetOrdinal("wordpressUserId")),
                WordpressUserLogin = reader.GetString(reader.GetOrdinal("wordpressUserLogin")),
                WordpressDisplayName = reader.GetString(reader.GetOrdinal("wordpressDisplayName")),
                WordpressUserEmail = reader.IsDBNull(reader.GetOrdinal("wordpressUserEmail"))
                    ? null : reader.GetString(reader.GetOrdinal("wordpressUserEmail")),
                WordpressUserPass = reader.GetString(reader.GetOrdinal("wordpressUserPass"))
            };
        }

        /// <summary>
        /// Verifica si una contraseña plana coincide con el hash almacenado por WordPress.
        /// Soporta hashes WordPress bcrypt, bcrypt estándar, phpass y md5 legado.
        /// </summary>
        /// <param name="password">Type: string - Contraseña ingresada por el usuario</param>
        /// <param name="wordpressHash">Type: string - Hash almacenado en WordPress</param>
        /// <returns>Type: bool - True si la contraseña es válida</returns>
        public bool Verify(string password, string wordpressHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(wordpressHash))
                return false;

            if (wordpressHash.StartsWith("$wp$2y$"))
                return VerifyWordpressBcrypt(password, wordpressHash);

            if (wordpressHash.StartsWith("$2y$") || wordpressHash.StartsWith("$2a$") || wordpressHash.StartsWith("$2b$"))
                return VerifyBcrypt(password, wordpressHash);

            if (wordpressHash.StartsWith("$P$") || wordpressHash.StartsWith("$H$"))
                return VerifyPhpass(password, wordpressHash);

            if (Regex.IsMatch(wordpressHash, "^[a-fA-F0-9]{32}$"))
            {
                var md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
                return FixedTimeEquals(md5, wordpressHash.ToLowerInvariant());
            }

            return false;
        }

        /// <summary>
        /// Verifica si una contraseña plana coincide con el hash almacenado por WordPress.
        /// Soporta hashes WordPress bcrypt, bcrypt estándar, phpass y md5 legado.
        /// </summary>
        /// <param name="password">Type: string - Contraseña ingresada por el usuario</param>
        /// <param name="wordpressHash">Type: string - Hash almacenado en WordPress</param>
        /// <returns>Type: bool - True si la contraseña es válida</returns>
        private static bool VerifyWordpressBcrypt(string password, string wordpressHash)
        {
            try
            {
                var bcryptHash = wordpressHash[3..];

                if (bcryptHash.StartsWith("$2y$"))
                    bcryptHash = "$2b$" + bcryptHash[4..];

                using var hmac = new HMACSHA384(Encoding.UTF8.GetBytes("wp-sha384"));
                var preHashedBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                var preHashedPassword = Convert.ToBase64String(preHashedBytes);

                return BCrypt.Net.BCrypt.Verify(preHashedPassword, bcryptHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica hashes nuevos de WordPress que usan prefijo $wp$2y$.
        /// WordPress prehashea la contraseña con HMAC-SHA384 antes de validar con bcrypt.
        /// </summary>
        /// <param name="password">Type: string - Contraseña ingresada por el usuario</param>
        /// <param name="wordpressHash">Type: string - Hash WordPress bcrypt</param>
        /// <returns>Type: bool - True si la contraseña coincide con el hash</returns>
        private static bool VerifyBcrypt(string password, string wordpressHash)
        {
            try
            {
                var bcryptHash = wordpressHash;

                if (bcryptHash.StartsWith("$2y$"))
                    bcryptHash = "$2b$" + bcryptHash[4..];

                return BCrypt.Net.BCrypt.Verify(password, bcryptHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Verifica hashes bcrypt estándar usados por WordPress o plugins compatibles.
        /// Convierte el prefijo $2y$ a $2b$ para compatibilidad con BCrypt.Net.
        /// </summary>
        /// <param name="password">Type: string - Contraseña ingresada por el usuario</param>
        /// <param name="wordpressHash">Type: string - Hash bcrypt almacenado</param>
        /// <returns>Type: bool - True si la contraseña coincide con el hash</returns>
        private bool VerifyPhpass(string password, string wordpressHash)
        {
            try
            {
                var calculatedHash = CryptPrivate(password, wordpressHash);
                return FixedTimeEquals(calculatedHash, wordpressHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reimplementa el algoritmo phpass usado por WordPress para calcular el hash de una contraseña.
        /// </summary>
        /// <param name="password">Type: string - Contraseña ingresada por el usuario</param>
        /// <param name="setting">Type: string - Hash phpass almacenado, usado también como configuración</param>
        /// <returns>Type: string - Hash calculado</returns>
        private string CryptPrivate(string password, string setting)
        {
            var output = "*0";

            if (setting.StartsWith(output))
                output = "*1";

            if (setting.Length < 12)
                return output;

            var idValue = setting[..3];

            if (idValue != "$P$" && idValue != "$H$")
                return output;

            var countLog2 = Itoa64.IndexOf(setting[3]);

            if (countLog2 < 7 || countLog2 > 30)
                return output;

            var count = 1 << countLog2;
            var salt = setting.Substring(4, 8);

            if (salt.Length != 8)
                return output;

            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var hashValue = MD5.HashData(Encoding.UTF8.GetBytes(salt).Concat(passwordBytes).ToArray());

            for (var i = 0; i < count; i++)
            {
                hashValue = MD5.HashData(hashValue.Concat(passwordBytes).ToArray());
            }

            return setting[..12] + Encode64(hashValue, 16);
        }

        /// <summary>
        /// Codifica bytes usando la tabla de caracteres personalizada de phpass.
        /// </summary>
        /// <param name="inputBytes">Type: byte[] - Bytes a codificar</param>
        /// <param name="count">Type: int - Cantidad de bytes a procesar</param>
        /// <returns>Type: string - Cadena codificada en formato phpass</returns>
        private static string Encode64(byte[] inputBytes, int count)
        {
            var output = new StringBuilder();
            var i = 0;

            while (i < count)
            {
                int value = inputBytes[i++];

                output.Append(Itoa64[value & 0x3f]);

                if (i < count)
                {
                    value |= inputBytes[i] << 8;
                }

                output.Append(Itoa64[(value >> 6) & 0x3f]);

                if (i >= count)
                {
                    break;
                }

                i++;

                if (i < count)
                {
                    value |= inputBytes[i] << 16;
                }

                output.Append(Itoa64[(value >> 12) & 0x3f]);

                if (i >= count)
                {
                    break;
                }

                i++;

                output.Append(Itoa64[(value >> 18) & 0x3f]);
            }

            return output.ToString();
        }

        /// <summary>
        /// Compara dos cadenas en tiempo constante para evitar ataques por timing.
        /// </summary>
        /// <param name="a">Type: string - Primera cadena</param>
        /// <param name="b">Type: string - Segunda cadena</param>
        /// <returns>Type: bool - True si ambas cadenas son iguales</returns>
        private static bool FixedTimeEquals(string a, string b)
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);

            return aBytes.Length == bBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}