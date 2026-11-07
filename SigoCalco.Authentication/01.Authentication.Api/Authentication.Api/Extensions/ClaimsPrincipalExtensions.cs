using System.Security.Claims;

namespace SigoCalco.Authentication.Api.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetEmail(this ClaimsPrincipal user)
        {
            var claims = user.Identities.Where(identity => identity.IsAuthenticated)
                .SelectMany(identity => identity.FindAll("email")).ToList();
            return claims.Count == 1 && !string.IsNullOrWhiteSpace(claims[0].Value)
                ? claims[0].Value.Trim() : null;
        }

        public static string? GetUserLogin(this ClaimsPrincipal user)
        {
            var claims = user.Identities
                .Where(identity => identity.IsAuthenticated)
                .SelectMany(identity => identity.FindAll("userLogin"))
                .ToList();

            return claims.Count == 1 && !string.IsNullOrWhiteSpace(claims[0].Value)
                ? claims[0].Value.Trim()
                : null;
        }
    }
}
