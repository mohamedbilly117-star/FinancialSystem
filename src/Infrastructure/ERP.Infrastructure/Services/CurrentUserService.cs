using System.Security.Claims;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ICurrentUserService"/> by reading claims off the
/// current <see cref="HttpContext"/>. In Blazor Server, the HttpContext
/// backing the initial SignalR circuit is available via
/// <see cref="IHttpContextAccessor"/> during the circuit's lifetime, which
/// is sufficient for populating audit fields and permission checks on every
/// use case invoked from a component.
///
/// Claim types used here (NameIdentifier, Office, Department) are
/// populated at sign-in time by the Security layer once the Users module
/// is implemented (a later milestone) - this class only reads them.
/// </summary>
public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserName => User?.Identity?.Name;

    public Guid? OfficeId
    {
        get
        {
            var value = User?.FindFirstValue(ApplicationClaimTypes.OfficeId);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? DepartmentId
    {
        get
        {
            var value = User?.FindFirstValue(ApplicationClaimTypes.DepartmentId);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }
}

/// <summary>Custom claim type names issued at sign-in (Users module, later milestone) to carry the organizational context described in Prompt 6.</summary>
public static class ApplicationClaimTypes
{
    public const string OfficeId = "erp:office_id";
    public const string DepartmentId = "erp:department_id";
}
