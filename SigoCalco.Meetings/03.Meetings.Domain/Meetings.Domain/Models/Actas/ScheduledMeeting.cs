namespace Meetings.Domain.Models.Actas;

public sealed class MeetingStatus
{
    public int MeetingStatusId { get; set; }
    public string Name { get; set; } = "";
}

// A scheduled meeting always belongs to an acta.
public sealed class ScheduledMeeting
{
    public int MeetingId { get; set; }
    public int ActaId { get; set; }
    public string Modality { get; set; } = "";
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string TimeZone { get; set; } = "America/Bogota";
    public string? LocationDescription { get; set; }
    public int OrganizerUserId { get; set; }
    public int MeetingStatusId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class MeetingReservation
{
    public int MeetingReservationId { get; set; }
    public int MeetingId { get; set; }
    public string? ExternalReservationId { get; set; }
    public string? ExternalRoomId { get; set; }
    public string ReservationStatus { get; set; } = "SOLICITADA";
    public DateTime RequestedAt { get; set; }
    public DateTime? SynchronizedAt { get; set; }
}

public sealed class MeetingCalendar
{
    public int MeetingCalendarId { get; set; }
    public int MeetingId { get; set; }
    public string? ExternalEventId { get; set; }
    public string OrganizerEmail { get; set; } = "";
    public bool CreateTeams { get; set; }
    public string? TeamsJoinUrl { get; set; }
    public string SyncStatus { get; set; } = "PENDIENTE";
    public string? LastError { get; set; }
    public DateTime? SynchronizedAt { get; set; }
}

