using System.Text.Json;

namespace Kazo.Fig.Documents;

/// <summary>One design FigCraft has open in a tab, as its <c>doc.list</c> command lists it.</summary>
/// <param name="Id">FigCraft's id of the design (<c>uid</c>): it stays the same while the tab is open, whatever its place or name.</param>
/// <param name="Name">The design's name, shown on its tab (<c>Home</c>; its save is <c>Home.figcraft</c>).</param>
/// <param name="Dirty">FigCraft says the design has unsaved changes.</param>
/// <param name="Active">It is the tab in front.</param>
public sealed record FigTab(long Id, string Name, bool Dirty, bool Active)
{
    /// <summary>
    /// Reads the answer of <c>doc.list</c>: <c>[{ index, id, name, path?, dirty, active }]</c>. The
    /// answer comes from the iframe: anything malformed is skipped.
    /// </summary>
    /// <param name="list">The command's result.</param>
    /// <returns>The tabs in FigCraft's order; empty when the answer is not a list.</returns>
    public static IReadOnlyList<FigTab> ListFrom(JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        List<FigTab> tabs = [];
        foreach (JsonElement entry in list.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || !entry.TryGetProperty("id", out JsonElement id)
                || id.ValueKind != JsonValueKind.Number
                || !id.TryGetInt64(out long tabId)
                || tabs.Exists(tab => tab.Id == tabId))
            {
                continue;
            }

            string name = entry.TryGetProperty("name", out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
            tabs.Add(new FigTab(tabId, name, Flag(entry, "dirty"), Flag(entry, "active")));
        }

        return tabs;
    }

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;
}
