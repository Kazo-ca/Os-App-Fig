namespace Kazo.Fig.Hosting;

/// <summary>The FigCraft theme (<c>ui.set { theme }</c>) for a Kazo OS colour scheme.</summary>
public static class FigCraftTheme
{
    /// <summary>
    /// <c>light</c> for the OS's light scheme, <c>dark</c> for anything else (the OS default is dark).
    /// </summary>
    /// <param name="scheme">The scheme <c>CraftFrame</c> reports (<c>light</c> or <c>dark</c>).</param>
    /// <returns>The value of FigCraft's <c>theme</c> preference.</returns>
    public static string Of(string? scheme) =>
        string.Equals(scheme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";
}
