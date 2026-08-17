using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace ERP.Infrastructure.Logging;

/// <summary>
/// Builds the single, centralized Serilog pipeline for the whole
/// application, implementing Prompt 3's Logging Architecture: Application
/// Logs, Security Logs, Performance Logs, User Activity Logs, Error Logs
/// and Configuration Logs. Rather than several unrelated ILogger sinks
/// scattered per module, every log event carries structured properties
/// (SourceContext, Office, UserId once ICurrentUserService is wired via an
/// enricher in a later milestone) so it can be filtered by log category
/// after the fact - the categories are a *view* over one stream, not
/// physically separate logging systems, keeping this simple to operate on
/// an offline LAN government server with no centralized log aggregation
/// platform available.
///
/// NOTE (Audit Logs specifically): the Audit Framework's business-level
/// audit trail (Prompt 6 - who approved/posted/reversed what, with old/new
/// values) is NOT implemented via Serilog. It is first-class, permanent,
/// queryable relational data (an AuditLog table per Prompt 4), because
/// Prompt 6 requires it to be reportable, filterable and permission-aware
/// - properties a rolling text/JSON log file cannot satisfy. Serilog here
/// covers *technical* logging (errors, performance, diagnostic user
/// activity), which is a distinct concern from that business audit trail.
/// </summary>
public static class SerilogConfiguration
{
    public static LoggerConfiguration BuildLoggerConfiguration(IConfiguration configuration, string environmentName)
    {
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.SignalR", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("Application", "GovERP")
            .Enrich.WithProperty("Environment", environmentName)
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/goverp-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 90, // government retention expectations exceed typical 7/30-day defaults
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}");

        // A database sink is configured only when a connection string is
        // present, so the scaffold still runs (and logs to console/file)
        // before a real SQL Server instance is provisioned during initial
        // environment setup.
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            loggerConfiguration = loggerConfiguration.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new Serilog.Sinks.MSSqlServer.MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    SchemaName = "logging",
                    AutoCreateSqlTable = true,
                });
        }

        return loggerConfiguration;
    }
}
