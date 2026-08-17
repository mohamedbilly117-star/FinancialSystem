using System.Globalization;

namespace ERP.Shared.Localization;

/// <summary>
/// Central constants for the approved localization strategy: Arabic (RTL)
/// is the primary/default language now; English (LTR) is future-scoped
/// (Prompt 8 - Localization; Prompt 11 - Localization). Keeping culture
/// codes here (rather than scattered "ar-EG" string literals across the
/// codebase) means adding an additional Arabic locale variant, or finally
/// switching on English, is a one-file change.
/// </summary>
public static class SupportedCultures
{
    public const string ArabicCultureCode = "ar-EG";
    public const string EnglishCultureCode = "en-US"; // future

    public static readonly CultureInfo Arabic = new(ArabicCultureCode);
    public static readonly CultureInfo English = new(EnglishCultureCode); // future

    /// <summary>The only culture enabled today. English is scaffolded but not activated, per the approved "Primary Language: Arabic. Future Language: English" decision.</summary>
    public static readonly IReadOnlyList<CultureInfo> Active = new[] { Arabic };

    public static readonly IReadOnlyList<CultureInfo> PlannedFuture = new[] { Arabic, English };

    public const string DefaultCultureCode = ArabicCultureCode;

    /// <summary>Text direction for the active/default culture - true (RTL) today for every screen (Prompt 8: "RTL Native").</summary>
    public const bool DefaultIsRightToLeft = true;
}
