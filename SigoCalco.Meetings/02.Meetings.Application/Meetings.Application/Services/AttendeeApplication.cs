
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Interfaces;
using Meetings.Domain.Models;
using System.Net.Mail;

namespace Meetings.Application.Services;

public class AttendeeApplication : IAttendeeApplication
{
    private readonly IAttendeeRepository _attendeeRepository;

    public AttendeeApplication(
        IAttendeeRepository attendeeRepository)
    {
        _attendeeRepository = attendeeRepository;
    }

    public async Task<AttendeeResponseDto> CreateAsync(
        CreateAttendeeDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(fullName) ||
            fullName.Length > 200)
        {
            throw new ArgumentException(
                "El nombre debe contener entre 1 y 200 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(email) ||
            email.Length > 254 ||
            !MailAddress.TryCreate(email, out var parsedEmail) ||
            !string.Equals(
                parsedEmail.Address,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "El correo electrónico no es válido.");
        }

        var existing = await _attendeeRepository
            .GetByEmailAsync(email, cancellationToken);

        if (existing is not null)
        {
            throw new AttendeeAlreadyExistsException();
        }

        var attendee = new Attendee
        {
            FullName = fullName,
            Email = email,
            IsActive = true
        };

        var created = await _attendeeRepository
            .CreateAsync(attendee, cancellationToken);

        return MapResponse(created);
    }

    public async Task<AttendeeResponseDto?> GetByIdAsync(
        long attendeeId,
        CancellationToken cancellationToken = default)
    {
        if (attendeeId <= 0)
        {
            throw new ArgumentException(
                "El identificador no es válido.");
        }

        var attendee = await _attendeeRepository
            .GetByIdAsync(attendeeId, cancellationToken);

        return attendee is null
            ? null
            : MapResponse(attendee);
    }

    private static AttendeeResponseDto MapResponse(
        Attendee attendee)
    {
        return new AttendeeResponseDto
        {
            AttendeeId = attendee.AttendeeId,
            UserId = attendee.UserId,
            FullName = attendee.FullName,
            Email = attendee.Email,
            IsActive = attendee.IsActive
        };
    }
}
