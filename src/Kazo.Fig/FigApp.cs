using Kazo.Os.App.Abstractions;

namespace Kazo.Fig;

/// <summary>
/// The app's manifest (doc/en/plan.md, decision 1). The build generates <c>manifest.json</c> from
/// this class (Kazo.Os.App.Build); it is never instantiated at run time.
/// </summary>
/// <remarks>
/// Work item 1 declares the base: identity, the window and its size, several windows at once, and
/// a window that stays on its screen (decision 4). Work item 2 adds the document formats (question
/// Q2), the OS dialogs and the capabilities they need.
/// </remarks>
[AppManifest]
public sealed class FigApp : AppDefinition
{
    /// <summary>
    /// Smallest usable width of FigCraft, in pixels: the toolbar, the layers panel, the properties
    /// panel and some canvas between them.
    /// </summary>
    public const int ContentMinWidth = 798;

    /// <summary>Smallest usable height of FigCraft, in pixels: toolbar, canvas and the panels' first sections.</summary>
    public const int ContentMinHeight = 480;

    /// <inheritdoc />
    public override string Id => "ca.kazo.fig";

    /// <inheritdoc />
    public override string Name => "Kazo Fig";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(FigWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "An interface design tool for screens, components and prototypes, built on FigCraft";

    /// <inheritdoc />
    public override string Category => "graphics";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Kazo OS Store must be v + this version.</remarks>
    public override string Version => "0.1.0";

    /// <inheritdoc />
    /// <remarks>
    /// Several windows at once: each one runs its own FigCraft in its own iframe, because the
    /// FigCraft canvas id is fixed and allows one instance per HTML document (doc/en/plan.md).
    /// </remarks>
    public override bool SingleInstancePerUser => false;

    /// <inheritdoc />
    /// <remarks>Work item 1 reads no file and opens no dialog; work item 2 adds what it needs.</remarks>
    public override IReadOnlyList<string> Capabilities => [];

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    /// <remarks>
    /// Sizes are the outer size of the OS window (title bar included): 1200 × 800 by default, room
    /// for FigCraft's two side panels and a canvas; at least FigCraft's minimum plus the frame.
    /// <see cref="WindowOptions.CanChangeScreen"/> is <see langword="false"/>: moving the window to
    /// another screen moves its element into another document, which reloads FigCraft's iframe and
    /// loses the open designs (Kazo OS ADR 0263, issue #2).
    /// </remarks>
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 1200,
        DefaultHeight = 800,
        MinWidth = ContentMinWidth + WindowChromeWidth,
        MinHeight = ContentMinHeight + WindowChromeHeight,
        CanChangeScreen = false,
    };

    // Borders and title bar of an OS window (the tallest title bar of the Kazo OS themes, 2.35rem).
    private const int WindowChromeWidth = 2;
    private const int WindowChromeHeight = 40;
}
