using Kazo.Fig.Documents;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Tests;

/// <summary>The files Kazo Fig knows, and FigCraft's rule for a design against a copy (decision 8, Q2 = A).</summary>
public sealed class FigFormatsTests
{
    [Theory]
    [InlineData("Home.figcraft")]
    [InlineData("home.FIGCRAFT")]
    [InlineData("design.json")]
    public void Figcraft_and_json_saves_are_the_design(string name)
    {
        Assert.True(FigFormats.IsDesignSave(name));
    }

    [Theory]
    [InlineData("Home.png")]
    [InlineData("Home@2x.jpg")]
    [InlineData("Home.svg")]
    [InlineData("Home.pdf")]
    [InlineData("Home.fig")]
    [InlineData("Home.variables.json")]
    [InlineData("Home.VARIABLES.JSON")]
    [InlineData("Home.variables.css")]
    [InlineData("Home")]
    public void Exports_and_the_variables_sidecar_are_copies(string name)
    {
        Assert.False(FigFormats.IsDesignSave(name));
    }

    [Theory]
    [InlineData("Home", "Home.figcraft")]
    [InlineData("  Landing page ", "Landing page.figcraft")]
    [InlineData("", "Untitled.figcraft")]
    [InlineData(null, "Untitled.figcraft")]
    public void A_design_saves_under_its_name(string? title, string expected)
    {
        Assert.Equal(expected, FigFormats.SaveNameOf(title));
    }

    [Fact]
    public void File_Open_is_told_from_import_by_the_json_designs_it_accepts()
    {
        PickRequest open = new(1, ".figcraft,.fig,.jam,.penpot,.sketch,.json,.svg,.png,.jpg,.jpeg,.webp,.gif,.tif,.tiff", false);
        PickRequest import = new(2, ".penpot,.svg,.png,.jpg,.jpeg,.webp,.gif,.tif,.tiff,.figcraft,.fig,.jam,.sketch", false);
        PickRequest place = new(3, ".png,.jpg,.jpeg,.webp,.gif,.tif,.tiff", false);

        Assert.True(FigFormats.IsOpenPicker(open));
        Assert.False(FigFormats.IsOpenPicker(import));
        Assert.False(FigFormats.IsOpenPicker(place));
    }

    [Fact]
    public void The_open_dialog_proposes_every_format_FigCraft_opens()
    {
        Assert.Equal(
            [".figcraft", ".fig", ".sketch", ".penpot", ".jam", ".json", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".gif", ".tif", ".tiff"],
            FigFormats.OpenExtensions);
    }
}
