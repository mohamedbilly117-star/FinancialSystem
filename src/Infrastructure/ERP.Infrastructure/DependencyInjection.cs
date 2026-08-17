using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.DateTime;
using ERP.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure;

/// <summary>Composition entry point for the general Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeService, DateTimeService>();
        services.AddScoped<INumberingSequenceService, NumberingSequenceService>();

        return services;
    }
}
