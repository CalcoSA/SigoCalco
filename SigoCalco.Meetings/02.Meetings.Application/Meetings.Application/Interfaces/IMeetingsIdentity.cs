namespace Meetings.Application.Interfaces;

// Implemented by API + a read-only Authentication adapter, never from request bodies.
public sealed record MeetingsIdentity(int UserId, IReadOnlyCollection<string> Roles, IReadOnlyCollection<string> CenterCodes);
public interface IMeetingsIdentity
{
    Task<MeetingsIdentity> GetAsync(CancellationToken ct);
}
public interface IAuthenticationDirectory
{
    Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct);
}

