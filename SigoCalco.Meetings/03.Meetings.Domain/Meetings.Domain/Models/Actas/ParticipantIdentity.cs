using Meetings.Domain.Exceptions;

namespace Meetings.Domain.Models.Actas;

public static class ParticipantIdentity
{
    // Authentication trims logins and compares them without case. Never match display names or email.
    public static string? NormalizeLogin(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > 60 || value.Any(char.IsControl)
            ? null : value.Trim().ToLowerInvariant();

    public static void RequireCompatible(Participant existing, Participant requested)
    {
        if (existing.ParticipantType != requested.ParticipantType ||
            NormalizeLogin(existing.UserLogin) != NormalizeLogin(requested.UserLogin))
            throw new MeetingsRequestException(409, "El correo ya pertenece a un participante con otra identidad o tipo. Revise el catálogo de participantes.");
    }
}
