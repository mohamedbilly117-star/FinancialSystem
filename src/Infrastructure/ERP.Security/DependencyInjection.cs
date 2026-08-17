using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ERP.Application.Common.Interfaces;
using ERP.Security.Authorization;
using ERP.Security.Services;

namespace ERP.Security;

/// <summary>
/// Composition entry point for the Security layer's framework-agnostic
/// pieces (authorization handler + dynamic policy provider). The actual
/// <c>services.AddIdentity&lt;ApplicationUser, ApplicationRole&gt;()
/// .AddEntityFrameworkStores&lt;ApplicationDbContext&gt;()</c> call is made
/// directly in ERP.Web's Program.cs (the composition root) rather than
/// here, because it requires the concrete ApplicationDbContext type from
/// ERP.Persistence - and ERP.Security intentionally does not reference
/// ERP.Persistence (see ERP.Security.csproj comments, avoiding a circular
/// project reference).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddAuthorizationCore();

        // Default password / lockout policy (Prompt 6: "Password Policies").
        // These starting values are sane government-grade defaults; per
        // Prompt 11 ("System Parameters" - Maximum Login Attempts, Password
        // Expiration) they become administrator-configurable through the
        // Configuration module in a later milestone, at which point this
        // Configure<IdentityOptions> call will read from persisted settings
        // instead of these literals.
        services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredUniqueChars = 4;

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = false; // government offices may share a functional mailbox; username is the unique identifier
            options.SignIn.RequireConfirmedAccount = false; // offline LAN deployment - no external email confirmation flow
        });

        return services;
    }
}
