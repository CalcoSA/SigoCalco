using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Microsoft.AspNetCore.Authorization;

namespace Meetings.Api.Security;

public sealed class PermissionAdministrationRequirement : IAuthorizationRequirement
{
    public const string Policy = "MeetingsPermissionAdministrator";
}

public sealed class PermissionAdministrationHandler(IMeetingsIdentity identity, IHttpContextAccessor accessor)
    : AuthorizationHandler<PermissionAdministrationRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionAdministrationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var actor = await identity.GetAsync(accessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (PermissionAdministration.IsAdministrator(actor)) context.Succeed(requirement);
        else context.Fail();
    }
}
