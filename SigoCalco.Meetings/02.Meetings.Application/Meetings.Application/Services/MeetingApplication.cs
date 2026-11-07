
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;

namespace Meetings.Application.Services;

public class MeetingApplication : IMeetingApplication
{
    private readonly IMeetingRepository _meetingRepository;

    public MeetingApplication(
        IMeetingRepository meetingRepository)
    {
        _meetingRepository = meetingRepository;
    }

    /// <summary>
    /// Crea una reunión con su acta en borrador.
    /// </summary>
    public async Task<CreateMeetingResponseDto> CreateAsync(
        CreateMeetingDto request,
        long createdByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (createdByUserId <= 0)
        {
            throw new ArgumentException(
                "El identificador del usuario no es válido.",
                nameof(createdByUserId));
        }

        // Validar título
        var title = request.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title) ||
            title.Length > 250)
        {
            throw new ArgumentException(
                "El título debe contener entre 1 y 250 caracteres.");
        }

        // Validar fechas
        if (request.StartAt == default ||
            request.EndAt == default ||
            request.EndAt <= request.StartAt)
        {
            throw new ArgumentException(
                "La fecha de finalización debe ser posterior al inicio.");
        }

        // Validar modalidad
        var modality = request.Modality?
            .Trim()
            .ToUpperInvariant();

        if (modality is not ("VIRTUAL" or "PRESENCIAL"))
        {
            throw new ArgumentException(
                "La modalidad debe ser VIRTUAL o PRESENCIAL.");
        }

        // Validar tipo de reunión
        var meetingKind = request.MeetingKind?
            .Trim()
            .ToUpperInvariant();

        if (!string.IsNullOrEmpty(meetingKind) &&
            meetingKind is not ("ORDINARIA" or "EXTRAORDINARIA"))
        {
            throw new ArgumentException(
                "El tipo debe ser ORDINARIA o EXTRAORDINARIA.");
        }

        // Crear una nueva acta en borrador
        var minute = new Minute
        {
            Title = title,
            Modality = modality,
            MeetingKind = meetingKind,
            Status = "DRAFT",
            CreatedByUserId = createdByUserId
        };

        // Crear la reunión asociada
        var meeting = new Meeting
        {
            Title = title,
            StartAt = request.StartAt.UtcDateTime,
            EndAt = request.EndAt.UtcDateTime,
            Modality = modality,
            Status = "SCHEDULED",
            OutlookSyncStatus = "PENDING",
            CreatedByUserId = createdByUserId,
            Minute = minute
        };

        // Guardar ambos registros en una transacción
        var createdMeeting =
            await _meetingRepository.CreateWithMinuteAsync(
                meeting,
                cancellationToken);

        return new CreateMeetingResponseDto
        {
            MeetingId = createdMeeting.MeetingId,
            MinuteId = createdMeeting.MinuteId,
            Status = createdMeeting.Status,
            OutlookSyncStatus = createdMeeting.OutlookSyncStatus
        };


    }

    public async Task<GetMeetingResponseDto?> GetByIdAsync(
        long meetingId,
        CancellationToken cancellationToken = default)
    {
        if (meetingId <= 0)
        {
            throw new ArgumentException(
                "El identificador de la reunión no es válido.",
                nameof(meetingId));
        }

        var meeting = await _meetingRepository.GetByIdAsync(
            meetingId,
            cancellationToken);

        if (meeting is null)
        {
            return null;
        }

        return new GetMeetingResponseDto
        {
            MeetingId = meeting.MeetingId,
            Title = meeting.Title,
            StartAt = meeting.StartAt,
            EndAt = meeting.EndAt,
            Modality = meeting.Modality,
            ReservationId = meeting.ReservationId,
            Status = meeting.Status,
            OutlookSyncStatus = meeting.OutlookSyncStatus,

            Minute = new MinuteSummaryDto
            {
                MinuteId = meeting.Minute.MinuteId,
                Title = meeting.Minute.Title,
                Status = meeting.Minute.Status,
                OccurredOn = meeting.Minute.OccurredOn,
                MeetingKind = meeting.Minute.MeetingKind
            }
        };
    }

}
