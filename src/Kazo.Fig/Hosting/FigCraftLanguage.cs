using System.Globalization;

namespace Kazo.Fig.Hosting;

/// <summary>
/// The language given to FigCraft (<c>pref.language</c>, and the module's <c>language</c> option).
/// </summary>
/// <remarks>
/// FigCraft resolves a BCP 47 tag itself, better than a fixed list would (doc/en/plan.md, reading
/// FigCraft): the exact catalog (<c>pt-BR</c>), else the base language, else a regional catalog
/// (<c>pt-PT</c> reads <c>pt-BR</c>), Latin American Spanish (<c>es-MX</c>) reads <c>es-419</c>,
/// Chinese picks Simplified or Traditional by script and region, and anything it lacks reads
/// English. So the window passes the Kazo user's culture name as it is.
/// </remarks>
public static class FigCraftLanguage
{
    /// <summary>FigCraft's fallback, always available.</summary>
    public const string English = "en";

    /// <summary>The tag for a culture: its name (<c>fr-CA</c>), or English for the invariant culture.</summary>
    /// <param name="culture">The Kazo user's culture.</param>
    /// <returns>A BCP 47 tag FigCraft accepts.</returns>
    public static string Of(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return string.IsNullOrEmpty(culture.Name) ? English : culture.Name;
    }
}
