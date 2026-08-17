using ERP.Application;
using ERP.Infrastructure;
using ERP.Infrastructure.Logging;
using ERP.Notifications;
using ERP.Persistence;
using ERP.Persistence.Context;
using ERP.Reporting;
using ERP.Security;
using ERP.Security.Identity;
using ERP.Shared.Localization;
using ERP.Web.Components;
using ERP.Workflow;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using MudBlazor.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Logging (Prompt 3 - Logging Architecture). Configured first so that
// every subsequent startup step - including failures during service
// registration - is captured.
// ---------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

Log.Logger = SerilogConfiguration
    .BuildLoggerConfiguration(builder.Configuration, builder.Environment.EnvironmentName)
    .CreateLogger();

try
{
    Log.Information("Starting GovERP Web Host");

    // -------------------------------------------------------------
    // Layer-by-layer service registration - one call per approved
    // project, per Prompt 3's Service Architecture / Prompt 14's
    // "compose every layer from the Web project" instruction.
    // -------------------------------------------------------------
    builder.Services.AddApplication();
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddInfrastructure();
    builder.Services.AddSecurity();
    builder.Services.AddWorkflow();
    builder.Services.AddNotifications();
    builder.Services.AddReporting();

    // ASP.NET Core Identity - registered here (not inside ERP.Security's
    // own DependencyInjection.cs) because AddEntityFrameworkStores<T>
    // needs the concrete ApplicationDbContext type from ERP.Persistence;
    // see ERP.Security.csproj's comments on avoiding a circular reference.
    builder.Services
        .AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            // Structural options only; the actual Password/Lockout *values*
            // are configured centrally in ERP.Security.DependencyInjection
            // (Configure<IdentityOptions>), evaluated after this call.
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Prompt 6 - Session Management: idle timeout during a normal government working day
    });

    // -------------------------------------------------------------
    // Presentation (Blazor Server - interactive Server render mode,
    // .NET 8 unified hosting model; no WebAssembly download, matching
    // the approved offline/LAN/desktop deployment model).
    // -------------------------------------------------------------
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddMudServices();

    builder.Services.AddLocalization();

    builder.Services.AddControllers(); // reserved for future export/download endpoints (PDF/Excel streaming - Prompt 9)

    var app = builder.Build();

    // -------------------------------------------------------------
    // Request pipeline
    // -------------------------------------------------------------
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    // Arabic-first, RTL-native localization (Prompt 8 / Prompt 11).
    // English is scaffolded in SupportedCultures for the future but not
    // yet added to SupportedCultures - switching it on later is a one-line
    // change to ERP.Shared.Localization.SupportedCultures.Active plus this
    // options list, with zero changes to individual pages.
    var localizationOptions = new RequestLocalizationOptions()
        .SetDefaultCulture(SupportedCultures.DefaultCultureCode)
        .AddSupportedCultures(SupportedCultures.Active.Select(c => c.Name).ToArray())
        .AddSupportedUICultures(SupportedCultures.Active.Select(c => c.Name).ToArray());
    app.UseRequestLocalization(localizationOptions);

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseAntiforgery();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GovERP Web Host terminated unexpectedly during startup");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
