using Meetings.Domain.Exceptions;

namespace Meetings.Domain.Models.Actas;

public static class ActaLifecycle
{
    public static readonly string[] States = ["BORRADOR", "PUBLICADA", "ANULADA"];
    public static void RequireDraft(string status)
    {
        if (status != "BORRADOR") throw new MeetingsRequestException(409, "Solo se puede editar un acta en BORRADOR.");
    }
    public static void RequireTransition(string current, string next)
    {
        if (!States.Contains(next)) throw MeetingsRequestException.Invalid("status debe ser BORRADOR, PUBLICADA o ANULADA.");
        if (!(current == "BORRADOR" && next is "PUBLICADA" or "ANULADA" || current == "PUBLICADA" && next == "ANULADA"))
            throw new MeetingsRequestException(409, "La transición de estado no está permitida.");
    }
}
