using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace ERP.Security.Authorization;

/// <summary>
/// An ASP.NET Core authorization requirement wrapping a single permission
/// string (e.g. "Banks.Approve"). Used with <c>[Authorize(Policy = "...")]</c>
/// on Blazor pages/components, and evaluated centrally by
/// <see cref="PermissionAuthorizationHandler"/> - satisfying Prompt 6's
/// "Security must be centralized. Authorization rules must never be
/// duplicated." Screen-level enforcement (Prompt 6's "Screen Security":
/// Modules, Menus, Pages, Forms, Buttons, Toolbar Actions, ...) is achieved
/// by tagging Blazor components/routes with the relevant policy; button
/// and section-level enforcement uses the same
/// <see cref="IPermissionService"/> injected directly into components.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission;
    }
}

/// <summary>
/// The single, central place permission decisions are made. Delegates to
/// <see cref="IPermissionService"/> (implemented against the Permission
/// Matrix data, built in the Security phase per the Prompt 13 roadmap) so
/// that both ASP.NET Core's declarative <c>[Authorize]</c> pipeline and any
/// imperative in-component checks (e.g. hiding a toolbar button) evaluate
/// permissions through the exact same logic - there is only ever one
/// permission engine in the system, per Prompt 6.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;
    private readonly ICurrentUserService _currentUserService;

    public PermissionAuthorizationHandler(
        IPermissionService permissionService,
        ICurrentUserService currentUserService)
    {
        _permissionService = permissionService;
        _currentUserService = currentUserService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (_currentUserService.UserId is not { } userId)
        {
            return; // unauthenticated - requirement not met, no exception here (Fails, doesn't throw)
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId, requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
