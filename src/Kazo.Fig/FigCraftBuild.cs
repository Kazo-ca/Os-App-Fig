namespace Kazo.Fig;

/// <summary>
/// The FigCraft web build this app carries in <c>wwwroot/figcraft/</c>, described by its
/// provenance file <c>figcraft.json</c> (written by <c>tools/vendor-figcraft.sh</c>, checked by
/// the tests).
/// </summary>
public static class FigCraftBuild
{
    /// <summary>
    /// The FigCraft version of the build. Kept equal to the <c>version</c> of
    /// <c>wwwroot/figcraft/figcraft.json</c> by a test, so the window can show it without reading
    /// the file at run time.
    /// </summary>
    public const string Version = "0.18.1";

    /// <summary>Folder of the build under the app's static files (<c>_content/Kazo.Fig/</c>).</summary>
    public const string Folder = "figcraft";
}
