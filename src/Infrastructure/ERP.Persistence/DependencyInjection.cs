using ERP.Application.Common.Interfaces;
using ERP.Persistence.Context;
using ERP.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Persistence;

/// <summary>
/// Composition entry point for the Persistence layer. Reads the
/// "DefaultConnection" connection string (SQL Server Express, per the
/// approved Database technology - Prompt 0 / Implementation Clarification
/// #1) from configuration, registers the audit interceptor, and exposes
/// <see cref="ApplicationDbContext"/> both as itself (for EF Core-specific
/// needs like migrations) and as <see cref="IApplicationDbContext"/> (for
/// the Application layer, per Dependency Inversion).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AuditableEntitySaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found. " +
                    "Configure it in appsettings.json (SQL Server Express, offline/LAN instance per the approved deployment model).");

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);

                // Government financial data over a 10+ year lifetime on a
                // LAN with unattended overnight batch jobs (period closing,
                // report generation) justifies a generous command timeout
                // and automatic retry on transient network blips, rather
                // than the short EF Core defaults tuned for cloud APIs.
                sql.CommandTimeout(120);
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
            });

            options.AddInterceptors(sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>());

#if DEBUG
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
#endif
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
