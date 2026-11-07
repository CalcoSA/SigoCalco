
using Meetings.Application.Dtos;

namespace Meetings.Application.Interfaces;

public interface IMeetingApplication
{
    /// <summary>
    /// Crea una reunión junto con su acta inicial.
    /// </summary>
    /// <param name="request">
    /// Datos de la reunión.
    /// </param>
    /// <param name="createdByUserId">
    /// Identificador del usuario autenticado.
    /// </param>
    /// <param name="cancellationToken">
    /// Token de cancelación.
    /// </param>
    /// <returns>
    /// Identificadores y estados de los registros creados.
    /// </returns>
    Task<CreateMeetingResponseDto> CreateAsync(
        CreateMeetingDto request,
        long createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene una reunión con su acta asociada.
    /// </summary>
    Task<GetMeetingResponseDto?> GetByIdAsync(
        long meetingId,
        CancellationToken cancellationToken = default);
}
