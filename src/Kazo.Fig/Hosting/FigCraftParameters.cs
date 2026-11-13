namespace Kazo.Fig.Hosting;

/// <summary>The query of FigCraft's iframe page (<c>CraftFrameOptions.Parameters</c>).</summary>
public static class FigCraftParameters
{
    /// <summary>
    /// The parameters for one window:
    /// <list type="bullet">
    /// <item><c>control</c>: FigCraft installs <c>window.figcraft</c>, the command API the module's
    /// proxy drives it with, only when the query asks for it;</item>
    /// <item><c>author</c>: the Kazo user's name, which signs comments and board notes (FigCraft
    /// trims it and keeps its first 200 characters);</item>
    /// <item><c>webgl</c>: forces the WebGL 2 renderer (the <c>--webgl</c> launch argument).</item>
    /// </list>
    /// Bare flags are empty values: FigCraft only looks for the name.
    /// </summary>
    /// <param name="author">The Kazo user's display name, if any.</param>
    /// <param name="webGl">Whether to force WebGL 2.</param>
    /// <returns>The parameters, in a stable order.</returns>
    public static IReadOnlyDictionary<string, string> For(string? author, bool webGl)
    {
        Dictionary<string, string> parameters = new(StringComparer.Ordinal) { ["control"] = string.Empty };
        string name = (author ?? string.Empty).Trim();
        if (name.Length > 0)
        {
            parameters["author"] = name;
        }

        if (webGl)
        {
            parameters["webgl"] = string.Empty;
        }

        return parameters;
    }
}
