using System.Net.Mail;
using SigoCalco.Authentication.Domain.Exceptions;

namespace SigoCalco.Authentication.Domain.Validators;

public static class UserEmail
{
    public const int MaxLength = 254;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var email = value.Trim();
        return email.Length <= MaxLength
            && !email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
            && MailAddress.TryCreate(email, out var address)
            && string.Equals(address.Address, email, StringComparison.Ordinal)
            && string.IsNullOrEmpty(address.DisplayName);
    }

    public static string Normalize(string? value)
    {
        if (!IsValid(value))
            throw new BusinessRuleException("Debe proporcionar un correo electrónico válido de hasta 254 caracteres.");
        return value!.Trim();
    }
}
