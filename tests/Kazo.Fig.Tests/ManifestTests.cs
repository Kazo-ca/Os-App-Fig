using Kazo.Os.App.Abstractions;

namespace Kazo.Fig.Tests;

/// <summary>The app's manifest class, the source of the generated manifest.json (doc/en/plan.md, decision 1).</summary>
public sealed class ManifestTests
{
    private readonly FigApp _app = new();

    [Fact]
    public void The_app_is_the_Kazo_Fig_window_app()
    {
        Assert.Equal("ca.kazo.fig", _app.Id);
        Assert.Equal("Kazo Fig", _app.Name);
        Assert.Equal("pub.kazo", _app.PublisherId);
        Assert.Equal("graphics", _app.Category);
        Assert.Equal(AppKind.Window, _app.Kind);
        Assert.Equal(typeof(FigWindow), _app.EntryPoint);
    }

    [Fact]
    public void Several_windows_can_run_at_once()
    {
        Assert.False(_app.SingleInstancePerUser);
    }

    [Fact]
    public void The_window_opens_at_1200_by_800_and_keeps_FigCraft_minimum()
    {
        WindowOptions window = Assert.IsType<WindowOptions>(_app.Window);
        Assert.Equal(1200, window.DefaultWidth);
        Assert.Equal(800, window.DefaultHeight);
        Assert.Equal(800, window.MinWidth);
        Assert.Equal(520, window.MinHeight);
    }

    [Fact]
    public void The_window_stays_on_its_screen_because_moving_it_reloads_FigCraft()
    {
        // Kazo OS ADR 0263, issue #2: another screen is another document, and the iframe reloads.
        WindowOptions window = Assert.IsType<WindowOptions>(_app.Window);
        Assert.False(window.CanChangeScreen);
    }

    [Fact]
    public void The_icon_is_declared_as_the_app_icon()
    {
        Assert.Equal(["icons/app.svg"], _app.Icons);
        AssetManifest icon = Assert.Single(_app.Assets);
        Assert.Equal("icons/app.svg", icon.Path);
        Assert.Equal("app-icon", icon.Role);
    }

    [Fact]
    public void Work_item_1_asks_for_no_capability_and_handles_no_file()
    {
        Assert.Empty(_app.Capabilities);
        Assert.Empty(_app.FileHandlers);
    }
}
