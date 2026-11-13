namespace Meetings.Domain.Models.Actas;

public sealed class ActaStatus
{
    public int ActaStatusId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class Acta
{
    public int ActaId { get; set; }
    public string ActaCode { get; set; } = "";
    public int ActaTypeId { get; set; }
    public int TemplateVersionId { get; set; }
    public string SolutionCenterCode { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly DocumentDate { get; set; }
    public int ActaStatusId { get; set; }
    public int CreatedByUser { get; set; }
    public int? UpdatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}

public sealed class ActaSection
{
    public int ActaSectionId { get; set; }
    public int ActaId { get; set; }
    public int TemplateVersionId { get; set; }
    public int TemplateSectionId { get; set; }
    public string? ContentJson { get; set; }
}

public sealed class Participant
{
    public int ParticipantId { get; set; }
    public string? UserLogin { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string ParticipantType { get; set; } = "";
}

public sealed class ActaParticipant
{
    public int ActaParticipantId { get; set; }
    public int ActaId { get; set; }
    public int ParticipantId { get; set; }
}

