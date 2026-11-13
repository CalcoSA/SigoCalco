namespace Meetings.Domain.Models.Actas;

public sealed class ActaType
{
    public int ActaTypeId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class Template
{
    public int TemplateId { get; set; }
    public int ActaTypeId { get; set; }
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public string? SolutionCenterCode { get; set; }
    public int CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public ActaType ActaType { get; set; } = null!;
    public List<TemplateVersion> Versions { get; set; } = [];
}

public sealed class TemplateVersion
{
    public int TemplateVersionId { get; set; }
    public int TemplateId { get; set; }
    public int ActaTypeId { get; set; }
    public int VersionNumber { get; set; }
    public int CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TemplateSection> Sections { get; set; } = [];
}

public sealed class TemplateSection
{
    public int TemplateSectionId { get; set; }
    public int TemplateVersionId { get; set; }
    public string Title { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string? ConfigurationJson { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
}

