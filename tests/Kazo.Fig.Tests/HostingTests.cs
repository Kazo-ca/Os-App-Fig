using System.Globalization;
using Kazo.Fig.Hosting;

namespace Kazo.Fig.Tests;

/// <summary>What the window gives FigCraft: the page's query, the language and the theme.</summary>
public sealed class HostingTests
{
    [Fact]
    public void The_page_always_asks_FigCraft_for_its_command_API()
    {
        // FigCraft installs window.figcraft only with ?control; the module's proxy needs it.
        IReadOnlyDictionary<string, string> parameters = FigCraftParameters.For(null, webGl: false);

        Assert.Equal(new Dictionary<string, string> { ["control"] = string.Empty }, parameters);
    }

    [Fact]
    public void The_user_s_name_signs_comments_and_webgl_forces_WebGL_2()
    {
        IReadOnlyDictionary<string, string> parameters = FigCraftParameters.For("  Ada Lovelace ", webGl: true);

        Assert.Equal("Ada Lovelace", parameters["author"]);
        Assert.Equal(string.Empty, parameters["webgl"]);
        Assert.Equal(string.Empty, parameters["control"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_user_with_no_name_signs_nothing(string? author)
    {
        Assert.False(FigCraftParameters.For(author, webGl: false).ContainsKey("author"));
    }

    [Theory]
    [InlineData("fr-CA", "fr-CA")]
    [InlineData("pt-PT", "pt-PT")]
    [InlineData("es-MX", "es-MX")]
    [InlineData("zh-TW", "zh-TW")]
    [InlineData("en", "en")]
    public void FigCraft_gets_the_culture_name_and_resolves_it_itself(string culture, string expected)
    {
        Assert.Equal(expected, FigCraftLanguage.Of(CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void The_invariant_culture_reads_English()
    {
        Assert.Equal(FigCraftLanguage.English, FigCraftLanguage.Of(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("light", "light")]
    [InlineData("LIGHT", "light")]
    [InlineData("dark", "dark")]
    [InlineData(null, "dark")]
    [InlineData("high-contrast", "dark")]
    public void FigCraft_follows_the_OS_scheme_not_the_browser(string? scheme, string expected)
    {
        Assert.Equal(expected, FigCraftTheme.Of(scheme));
    }
}
