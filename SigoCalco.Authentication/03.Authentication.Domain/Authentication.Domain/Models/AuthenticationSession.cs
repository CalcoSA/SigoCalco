using SigoCalco.Authentication.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace SigoCalco.Authentication.Domain.Models
{
    public class AuthenticationSession
    {
        public string IdSession { get; set; } = null!;
        public int IdUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RefreshTokenHash { get; set; }

        public void EnsureActive(DateTime now)
        {
            if (RevokedAt.HasValue)
                throw new SessionException("SESSION_REVOKED", "La sesión fue cerrada.", ExpiresAt);
            if (ExpiresAt <= now || RefreshTokenHash is null)
                throw new SessionException("SESSION_EXPIRED", "La sesión venció por inactividad.", ExpiresAt);
        }

        public void RegisterActivity(DateTime now, TimeSpan idleTimeout)
        {
            EnsureActive(now);
            var nextActivityAt = LastActivityAt.AddSeconds(Math.Min(15, idleTimeout.TotalSeconds / 10));
            if (now < nextActivityAt)
                throw new SessionException("ACTIVITY_THROTTLED", "Espere antes de informar más actividad.",
                    ExpiresAt, (int)Math.Ceiling((nextActivityAt - now).TotalSeconds));

            LastActivityAt = now;
            ExpiresAt = now.Add(idleTimeout);
        }

        public void RotateRefreshToken(DateTime now, string currentHash, string newHash)
        {
            EnsureActive(now);
            if (RefreshTokenHash is null || currentHash.Length != 64 ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(RefreshTokenHash), Encoding.ASCII.GetBytes(currentHash)))
                throw new SessionException("REFRESH_INVALID", "El token de renovación no es válido o ya fue utilizado.");

            RefreshTokenHash = newHash;
        }

        public void Revoke(DateTime now)
        {
            EnsureActive(now);
            RevokedAt = now;
            RefreshTokenHash = null;
        }
    }
}
