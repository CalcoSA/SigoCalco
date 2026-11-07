
using Meetings.Domain.Models;

namespace Meetings.Domain.Interfaces;

public interface IMeetingRepository
{
    /// <summary>
    /// Obtiene una reunión por su identificador.
    /// </summary>
    /// <param name="meetingId">
    /// Type: long - Identificador de la reunión.
    /// </param>
    /// <param name="cancellationToken">
    /// Type: CancellationToken - Token de cancelación.
    /// </param>
    /// <returns>
    /// Type: Meeting? - Reunión encontrada o null.
    /// </returns>
    Task<Meeting?> GetByIdAsync(
        long meetingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una reunión y su nueva acta
    /// dentro de una única transacción.
    /// </summary>
    /// <param name="meeting">
    /// Type: Meeting - Reunión con su acta nueva.
    /// </param>
    /// <param name="cancellationToken">
    /// Type: CancellationToken - Token de cancelación.
    /// </param>
    /// <returns>
    /// Type: Meeting - Reunión creada con identificadores.
    /// </returns>
    Task<Meeting> CreateWithMinuteAsync(
        Meeting meeting,
        CancellationToken cancellationToken = default);
}
