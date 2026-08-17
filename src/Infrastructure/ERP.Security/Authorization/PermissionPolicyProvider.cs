using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ERP.Security.Authorization;

/// <summary>
/// Standard ASP.NET Core authorization requires every policy name used in
/// <c>[Authorize(Policy = "...")]</c> to be pre-registered at startup via
/// <c>AddAuthorization(options => options.AddPolicy(...))</c>. For a system
/// whose entire Permission Matrix (Prompt 6) is configurable data spanning
/// dozens of modules added over a 10+ year lifetime (Prompt 2: "No
/// configurable rule may require source code changes"), hand-registering
/// every "{Resource}.{Action}" combination at startup would violate that
/// principle every time a new module is added.
///
/// This provider intercepts any policy name that is not already a known,
/// explicitly-registered policy and treats it as a permission string,
/// dynamically constructing a policy backed by a single
/// <see cref="PermissionRequirement"/>. A Blazor page can therefore write
/// <c>[Authorize(Policy = "Banks.Approve")]</c> the moment the "Banks"
/// module is implemented, with zero changes to this file or to Program.cs.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Let explicitly-registered policies (if any are ever added for
        // non-permission scenarios) take precedence over the dynamic
        // permission-based fallback.
        var existingPolicy = await _fallbackPolicyProvider.GetPolicyAsync(policyName);
        if (existingPolicy is not null)
        {
            return existingPolicy;
        }

        var policy = new AuthorizationPolicyBuilder();
        policy.AddRequirements(new PermissionRequirement(policyName));
        return policy.Build();
    }
}
