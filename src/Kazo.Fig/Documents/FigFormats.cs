using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Documents;

/// <summary>
/// The files Kazo Fig knows (doc/en/plan.md, decision 8, Benoit's Q2 = A): the design files it
/// becomes the app of, the files it only offers in "Open with", and the copies FigCraft exports.
/// </summary>
/// <remarks>
/// FigCraft saves a design as <c>.figcraft</c> (or <c>.json</c>, the same content). Every other file
/// it hands over is a copy that never becomes the design's file: rendered images, SVG, PDF,
/// <c>.fig</c> copies and the variables exports, whose <c>.variables.json</c> name ends in
/// <c>.json</c> all the same (<see cref="IsDesignSave"/>, FigCraft's own rule in
/// <c>apps/figcraft-web/src/hostmode.rs</c>).
/// </remarks>
public static class FigFormats
{
    /// <summary>The extension of the files FigCraft saves a design as.</summary>
    public const string DesignExtension = ".figcraft";

    /// <summary>The suffix of FigCraft's variables export, a copy whatever its <c>.json</c> extension.</summary>
    public const string VariablesSuffix = ".variables.json";

    /// <summary>Gets every format, with what Kazo Fig does with it.</summary>
    public static CraftDocumentFormats Formats { get; } = new(
    [
        // Default app (open and edit): the design files (Q2 = A).
        new(".figcraft", "application/x-figcraft", IsDefault: true, IsCopy: false),
        new(".fig", "application/x-fig", IsDefault: true, IsCopy: false),
        new(".sketch", "application/x-sketch", IsDefault: true, IsCopy: false),
        new(".penpot", "application/x-penpot", IsDefault: true, IsCopy: false),
        new(".jam", "application/x-jam", IsDefault: true, IsCopy: false),

        // "Open with" only: another app stays their default (Q2 = A). FigCraft also exports some of
        // them as copies; IsDesignSave tells a copy from the design.
        new(".json", "application/json", IsDefault: false, IsCopy: false),
        new(".svg", "image/svg+xml", IsDefault: false, IsCopy: true),
        new(".png", "image/png", IsDefault: false, IsCopy: true),
        new(".jpg", "image/jpeg", IsDefault: false, IsCopy: true),
        new(".jpeg", "image/jpeg", IsDefault: false, IsCopy: true),
        new(".webp", "image/webp", IsDefault: false, IsCopy: true),
        new(".gif", "image/gif", IsDefault: false, IsCopy: false),
        new(".tif", "image/tiff", IsDefault: false, IsCopy: false),
        new(".tiff", "image/tiff", IsDefault: false, IsCopy: false),

        // Copies FigCraft exports and never opens.
        new(".pdf", "application/pdf", IsDefault: false, IsCopy: true, Opens: false),
    ]);

    /// <summary>Gets the extensions the OS Open dialog proposes, in the order above.</summary>
    public static IReadOnlyList<string> OpenExtensions => Formats.OpenExtensions;

    /// <summary>Gets the file associations of the manifest.</summary>
    public static IReadOnlyList<FileHandler> FileHandlers => Formats.FileHandlers;

    /// <summary>
    /// Whether a file FigCraft saves is the design itself (File › Save, Save as) rather than a copy:
    /// a <c>.figcraft</c> or <c>.json</c> name that is not a variables export.
    /// </summary>
    /// <param name="name">The file name FigCraft gave.</param>
    /// <returns><see langword="true"/> for the design.</returns>
    public static bool IsDesignSave(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.EndsWith(VariablesSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string extension = CraftDocumentFormats.ExtensionOf(name);
        return extension is DesignExtension or ".json";
    }

    /// <summary>
    /// The name FigCraft gives the save of a design titled <paramref name="title"/> (its tab):
    /// <c>Home.figcraft</c> for <c>Home</c>.
    /// </summary>
    /// <param name="title">The design's name in FigCraft (<c>doc.list</c>).</param>
    /// <returns>The file name.</returns>
    public static string SaveNameOf(string? title)
    {
        string name = (title ?? string.Empty).Trim();
        return (name.Length == 0 ? "Untitled" : name) + DesignExtension;
    }

    /// <summary>
    /// Whether a FigCraft picker is its File › Open (designs) rather than Import, Place image or an
    /// image fill: only Open accepts <c>.json</c> designs.
    /// </summary>
    /// <param name="request">The picker FigCraft opened.</param>
    /// <returns><see langword="true"/> for File › Open.</returns>
    public static bool IsOpenPicker(PickRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.AcceptsAny([".json"]) && request.AcceptsAny([DesignExtension]);
    }
}
