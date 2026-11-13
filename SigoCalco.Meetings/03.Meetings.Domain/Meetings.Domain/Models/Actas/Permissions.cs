namespace Meetings.Domain.Models.Actas;

public sealed class MeetingsPermission
{
    public int PermissionId { get; set; }
    public string PermissionKey { get; set; } = "";
    public string PermissionValue { get; set; } = "";
}

public sealed class ActaCenterPermission
{
    public int ActaCenterPermissionId { get; set; }
    public int ActaId { get; set; }
    public string SolutionCenterCode { get; set; } = "";
    public string Permission { get; set; } = "";
    public int GrantedByUserId { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class ActaUserPermission
{
    public int ActaUserPermissionId { get; set; }
    public int ActaId { get; set; }
    public int UserId { get; set; }
    public string Permission { get; set; } = "";
    public int GrantedByUserId { get; set; }
    public DateTime GrantedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public sealed class MeetingsLog
{
    public long LogId { get; set; }
    public string Action { get; set; } = "";
    public string Module { get; set; } = "";
    public string Description { get; set; } = "";
    public string UserName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

