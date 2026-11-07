namespace Meetings.Domain.Exceptions;

public class AttendeeAlreadyExistsException : Exception
{
    public AttendeeAlreadyExistsException()
        : base("Ya existe un asistente registrado con ese correo electrónico.")
    {
    }
}