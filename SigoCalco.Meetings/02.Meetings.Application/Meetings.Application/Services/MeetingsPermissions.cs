using System.Text;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;

namespace Meetings.Application.Services;

public sealed record TemplateVisibility(bool Global, bool OwnCenters, bool AllCenters)
{
    public bool Any => Global || OwnCenters || AllCenters;
}
public static class MeetingsPermissions
{
    public const string GlobalManage = "PLANTILLAS_GLOBALES_GESTION";
    public const string GlobalRead = "PLANTILLAS_GLOBALES_LECTURA";
    public const string CenterManage = "PLANTILLAS_CENTRO_GESTION";
    public const string CenterRead = "PLANTILLAS_CENTRO_LECTURA";
    public const string ActasManage = "ACTAS_GESTION";
    public const string ActasRead = "ACTAS_LECTURA";
    public static readonly string[] Keys = [GlobalManage, GlobalRead, CenterManage, CenterRead, ActasManage, ActasRead];
    public static readonly string[] ActiveTypeCodes = ["REUNION", "ADMINISTRATIVA"];
    public static string NormalizeRole(string value) => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
    public static bool Allowed(IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor, string key) =>
        permissions.TryGetValue(key, out var value) && (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeRole).Intersect(actor.Roles.Select(NormalizeRole), StringComparer.Ordinal).Any();
    public static TemplateVisibility Visibility(IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor)
    {
        var globalManage = Allowed(permissions, actor, GlobalManage);
        return new(globalManage || Allowed(permissions, actor, GlobalRead),
            Allowed(permissions, actor, CenterManage) || Allowed(permissions, actor, CenterRead), globalManage);
    }
    public static void RequireRead(Template template, IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor)
    {
        var access = Visibility(permissions, actor);
        if (!(template.Scope == "GLOBAL" ? access.Global : access.AllCenters || access.OwnCenters && OwnsCenter(actor, template.SolutionCenterCode)))
            throw MeetingsRequestException.Forbidden();
    }
    public static void RequireManage(Template template, IReadOnlyDictionary<string, string> permissions, MeetingsIdentity actor)
    {
        if (!Allowed(permissions, actor, template.Scope == "GLOBAL" ? GlobalManage : CenterManage) ||
            template.Scope == "CENTRO" && !OwnsCenter(actor, template.SolutionCenterCode)) throw MeetingsRequestException.Forbidden();
    }
    public static bool OwnsCenter(MeetingsIdentity actor, string? center) => actor.CenterCodes.Contains(center ?? "", StringComparer.OrdinalIgnoreCase);
}
