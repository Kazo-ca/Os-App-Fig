using System.Text.Json;
using System.Xml.Linq;

namespace Kazo.Fig.Tests;

/// <summary>
/// The licenses travel with the app (work item 4, THIRD-PARTY-NOTICES.md): the project links the
/// repository's license and notices into the package's web root, FigCraft's own notices are in
/// its build folder, and the notices name the FigCraft version actually bundled.
/// </summary>
public sealed class LegalFilesTests
{
    private static readonly string Root = FindRepositoryRoot();

    private static readonly string FigCraftFolder = Path.Combine(Root, "src", "Kazo.Fig", "wwwroot", "figcraft");

    [Theory]
    [InlineData("LICENSE", "wwwroot/legal/LICENSE.txt")]
    [InlineData("THIRD-PARTY-NOTICES.md", "wwwroot/legal/THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md", "wwwroot/legal/THIRD-PARTY-NOTICES-fr.md")]
    public void The_project_puts_the_repository_legal_files_in_the_package(string file, string link)
    {
        XDocument project = XDocument.Load(Path.Combine(Root, "src", "Kazo.Fig", "Kazo.Fig.csproj"));

        // A Content item linked under wwwroot/ is a static web asset: KazoOsPack packs it.
        XElement? item = project.Descendants("Content")
            .FirstOrDefault(element => (string?)element.Attribute("Link") == link);
        Assert.NotNull(item);
        Assert.Equal($"../../{file}", (string?)item.Attribute("Include"));
        Assert.True(File.Exists(Path.Combine(Root, file)), $"{file} is missing at the repository root");
    }

    [Theory]
    [InlineData("LICENSE-MIT")]
    [InlineData("LICENSE-APACHE")]
    [InlineData("NOTICE")]
    public void FigCraft_ships_its_own_notices(string file)
    {
        Assert.True(File.Exists(Path.Combine(FigCraftFolder, file)), $"figcraft/{file} is missing");
    }

    [Fact]
    public void The_notices_count_the_font_licenses_FigCraft_ships()
    {
        // FigCraft's web build embeds craft-fonts: one OFL text per font family, all shipped.
        int fonts = Directory.GetFiles(FigCraftFolder, "OFL-*.txt").Length;
        Assert.True(fonts > 0, "figcraft/ has no OFL-*.txt");
        Assert.Contains($"`OFL-*.txt` ({fonts} files)", File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES.md")), StringComparison.Ordinal);
        Assert.Contains($"`OFL-*.txt` ({fonts} fichiers)", File.ReadAllText(Path.Combine(Root, "THIRD-PARTY-NOTICES-fr.md")), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("THIRD-PARTY-NOTICES.md")]
    [InlineData("THIRD-PARTY-NOTICES-fr.md")]
    public void The_notices_name_the_bundled_FigCraft_version(string file)
    {
        // Moving to another FigCraft version without updating the notices fails here.
        using JsonDocument provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(FigCraftFolder, "figcraft.json")));
        string version = provenance.RootElement.GetProperty("version").GetString()!;

        string notices = File.ReadAllText(Path.Combine(Root, file));
        Assert.Contains($"| {version}, ", notices, StringComparison.Ordinal);
        Assert.Contains($"kazo/v{version}", notices, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("description.en.md", "FigCraft")]
    [InlineData("description.fr.md", "FigCraft")]
    [InlineData("description.en.md", "ArtCraft")]
    [InlineData("description.fr.md", "ArtCraft")]
    public void The_store_listing_credits_FigCraft(string file, string name)
    {
        Assert.Contains(name, File.ReadAllText(Path.Combine(Root, "store", file)), StringComparison.Ordinal);
    }

    [Fact]
    public void The_store_tags_are_ones_the_catalog_accepts()
    {
        // Os-AppMarketPlace refuses any other tag (lower case, digits and '-', 30 characters at most):
        // Kazo Word 0.1.0 had "word processor" and its catalog folder had to be corrected by hand.
        using JsonDocument store = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "store", "store.json")));
        foreach (JsonElement tag in store.RootElement.GetProperty("tags").EnumerateArray())
        {
            Assert.Matches("^[a-z0-9][a-z0-9-]{0,29}$", tag.GetString());
        }
    }

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Kazo.Fig.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Kazo.Fig.slnx not found above " + AppContext.BaseDirectory);
    }
}
