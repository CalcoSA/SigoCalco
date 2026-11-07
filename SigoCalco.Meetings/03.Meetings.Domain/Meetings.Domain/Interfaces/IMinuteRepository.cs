
using Meetings.Domain.Models;

namespace Meetings.Domain.Interfaces;

public interface IMinuteRepository
{
    /// <summary>
    /// Obtiene un acta por su identificador.
    /// </summary>
    /// <param name="minuteId">Identificador del acta.</param>
    /// <param name="cancellationToken">
    /// Token de cancelación.
    /// </param>
    /// <returns>Acta encontrada o null.</returns>
    Task<Minute?> GetByIdAsync(
        long minuteId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un acta nueva e independiente.
    /// </summary>
    Task<Minute> CreateAsync(
        Minute minute,
        CancellationToken cancellationToken = default);
}
