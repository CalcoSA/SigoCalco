using System.Text.Json.Serialization;

namespace Meetings.Application.Dtos;

public sealed record PermissionDto(int PermissionId, string PermissionKey, IReadOnlyList<string> Roles);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AddPermissionRoleDto(string RoleName);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdatePermissionRolesDto(IReadOnlyList<string>? Roles);
