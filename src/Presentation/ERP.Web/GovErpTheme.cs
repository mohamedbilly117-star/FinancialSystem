using MudBlazor;

namespace ERP.Web;

/// <summary>
/// Starting point for the application's MudBlazor <see cref="MudTheme"/>.
/// This scaffolding milestone defines only a professional, government-
/// appropriate base palette and typography so the application shell
/// renders correctly; the full Visual Design System deliverable approved
/// in Prompt 8 (Typography, Spacing, Icons, Buttons, Cards, Panels,
/// Tables, Dialogs, Badges, Alerts, color usage principles, Dark Mode
/// readiness) is implemented in full during the dedicated UI
/// implementation milestone.
/// </summary>
public static class GovErpTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#0B3D5C",     // deep institutional blue - conveys trust/government without being a specific flag color
            Secondary = "#B08D2B",   // muted gold accent, used sparingly (approvals/highlights)
            AppbarBackground = "#0B3D5C",
            Background = "#F4F6F8",
            Surface = "#FFFFFF",
            Success = "#2E7D32",
            Warning = "#B26A00",
            Error = "#B3261E",
            Info = "#0288D1",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "6px",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = new[] { "Segoe UI", "Tahoma", "Cairo", "Arial", "sans-serif" },
            },
        },
    };
}
