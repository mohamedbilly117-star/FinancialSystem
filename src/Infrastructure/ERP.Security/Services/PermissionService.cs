using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Interfaces;
using ERP.Security.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ERP.Security.Services;

/// <summary>
/// Prompt 6 - Permission Engine, implemented. This is the ONE place
/// permission decisions actually get made (Prompt 6: "Security must be
/// centralized. Authorization rules must never be duplicated.") - both
/// <see cref="Authorization.PermissionAuthorizationHandler"/> (the
/// declarative <c>[Authorize(Policy = "...")]</c> pipeline) and any
/// imperative in-component check (e.g. hiding a toolbar button) go
/// through this same class via <see cref="IPermissionService"/>.
///
/// Resolution path: User -&gt; Role names (via
/// <see cref="UserManager{TUser}"/>) -&gt; Role Ids (via
/// <see cref="RoleManager{TRole}"/>) -&gt; granted Permission codes (via
/// <see cref="RolePermission"/> joined to <see cref="Permission"/> through
/// <see cref="IApplicationDbContext"/>). Deliberately built on
/// <c>UserManager</c>/<c>RoleManager</c> rather than querying Identity's
/// raw join tables directly - these are ASP.NET Core Identity's own
/// supported, idiomatic APIs for exactly this lookup, already available
/// since ERP.Security references
/// <c>Microsoft.AspNetCore.Identity.EntityFrameworkCore</c>.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _dbContext;

    public PermissionService(UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        // Prompt 6 - Identity Management: "Disabled Users" have no
        // permissions regardless of role assignments - checked before
        // even looking at roles, so a disabled account can never slip
        // through via a role that still has grants.
        if (user is null || user.IsDisabled)
        {
            return false;
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        if (roleNames.Count == 0)
        {
            return false;
        }

        var roleIds = await _roleManager.Roles
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return false;
        }

        var hasPermission = await (
            from rolePermission in _dbContext.RolePermissions
            join catalogPermission in _dbContext.Permissions on rolePermission.PermissionId equals catalogPermission.Id
            where roleIds.Contains(rolePermission.RoleId)
                  && !rolePermission.IsDeleted
                  && !catalogPermission.IsDeleted
                  && catalogPermission.Code == permission
            select rolePermission.Id
        ).AnyAsync(cancellationToken);

        return hasPermission;
    }

    public async Task AuthorizeAsync(Guid userId, string permission, CancellationToken cancellationToken = default)
    {
        var hasPermission = await HasPermissionAsync(userId, permission, cancellationToken);

        if (!hasPermission)
        {
            throw new ForbiddenAccessException($"User does not have the required permission: '{permission}'.");
        }
    }
}
