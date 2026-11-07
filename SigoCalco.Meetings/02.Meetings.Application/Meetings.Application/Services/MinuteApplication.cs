
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;

namespace Meetings.Application.Services;

public class MinuteApplication : IMinuteApplication
{
    private readonly IMinuteRepository _minuteRepository;

    public MinuteApplication(
        IMinuteRepository minuteRepository)
    {
        _minuteRepository = minuteRepository;
    }

    public async Task<MinuteResponseDto> CreateAsync(
        CreateMinuteDto request,
        long createdByUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (createdByUserId <= 0)
            throw new ArgumentException(
                "El usuario creador no es válido.");

        var title = request.Title?.Trim();

        if (string.IsNullOrWhiteSpace(title) ||
            title.Length > 250)
            throw new ArgumentException(
                "El título debe contener entre 1 y 250 caracteres.");

        var modality = request.Modality?
            .Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(modality) &&
            modality is not ("VIRTUAL" or "PRESENCIAL"))
            throw new ArgumentException(
                "La modalidad no es válida.");

        var meetingKind = request.MeetingKind?
            .Trim().ToUpperInvariant();

        if (!string.IsNullOrEmpty(meetingKind) &&
            meetingKind is not ("ORDINARIA" or "EXTRAORDINARIA"))
            throw new ArgumentException(
                "El tipo de reunión no es válido.");

        if (request.MinuteTypeId <= 0 ||
            request.TemplateId <= 0)
            throw new ArgumentException(
                "Los identificadores de tipo y plantilla deben ser positivos.");

        var location = request.LocationDescription?.Trim();

        if (location?.Length > 255)
            throw new ArgumentException(
                "El lugar no puede superar 255 caracteres.");

        var minute = new Minute
        {
            Title = title,
            MinuteTypeId = request.MinuteTypeId,
            TemplateId = request.TemplateId,
            OccurredOn = request.OccurredOn,
            ActualStartTime = request.ActualStartTime,
            ActualEndTime = request.ActualEndTime,
            Modality = modality,
            LocationDescription = location,
            MeetingKind = meetingKind,
            Status = "DRAFT",
            CreatedByUserId = createdByUserId
        };

        var created = await _minuteRepository.CreateAsync(
            minute, cancellationToken);

        return MapResponse(created);
    }

    public async Task<MinuteResponseDto?> GetByIdAsync(
        long minuteId,
        CancellationToken cancellationToken = default)
    {
        if (minuteId <= 0)
            throw new ArgumentException(
                "El identificador del acta no es válido.");

        var minute = await _minuteRepository.GetByIdAsync(
            minuteId, cancellationToken);

        return minute is null ? null : MapResponse(minute);
    }

    private static MinuteResponseDto MapResponse(Minute minute)
    {
        return new MinuteResponseDto
        {
            MinuteId = minute.MinuteId,
            Title = minute.Title,
            MinuteTypeId = minute.MinuteTypeId,
            TemplateId = minute.TemplateId,
            OccurredOn = minute.OccurredOn,
            ActualStartTime = minute.ActualStartTime,
            ActualEndTime = minute.ActualEndTime,
            Modality = minute.Modality,
            LocationDescription = minute.LocationDescription,
            MeetingKind = minute.MeetingKind,
            Status = minute.Status,
            CreatedByUserId = minute.CreatedByUserId,
            CreatedAt = minute.CreatedAt
        };
    }
}
