using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace ERP.Reporting;

/// <summary>Marker used by ERP.ArchitectureTests (<c>typeof(IReportingMarker).Assembly</c>).</summary>
public interface IReportingMarker
{
}

/// <summary>
/// Composition entry point for the centralized Reporting Architecture
/// (Prompt 3 / Prompt 9). Individual report definitions (General Ledger,
/// Trial Balance, Bank/Treasury/Cards/Advances/Aid reports, ...) are built
/// module-by-module alongside their owning business module, per the
/// approved Implementation Roadmap (Prompt 13) - this project currently
/// only performs the mandatory, global QuestPDF license configuration and
/// exposes the extension point every future report generator will be
/// registered through.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // QuestPDF requires exactly one process-wide license declaration.
        // Community is the free tier, appropriate for this government/
        // internal system; revisit if QuestPDF's licensing terms or the
        // organization's revenue/size profile changes over the system's
        // 10+ year lifetime.
        QuestPDF.Settings.License = LicenseType.Community;

        // Future registrations: IPdfReportGenerator / IExcelReportGenerator
        // implementations per report (QuestPDF- and ClosedXML-backed
        // respectively), plus the shared RTL-aware layout/typography
        // helpers referenced in ERP.Reporting.csproj's comments.
        return services;
    }
}
