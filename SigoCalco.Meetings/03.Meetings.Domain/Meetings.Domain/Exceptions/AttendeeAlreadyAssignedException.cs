
namespace Meetings.Domain.Exceptions;

public class AttendeeAlreadyAssignedException : Exception
{
    public AttendeeAlreadyAssignedException()
        : base("El asistente ya está asociado a este registro.")
    {
    }
}
