using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application;

/// <summary>
/// Composition entry point for the Application layer. ERP.Web's
/// Program.cs calls <c>services.AddApplication()</c> once; every future
/// module's FluentValidation validators and AutoMapper profiles are picked
/// up automatically via assembly scanning, so adding a new module never
/// requires touching this file again.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(IApplicationMarker).Assembly;

        services.AddValidatorsFromAssembly(applicationAssembly);

        services.AddAutoMapper(applicationAssembly);

        return services;
    }
}
