using System.Text.Json;
using Kazo.Fig.Documents;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Tests;

/// <summary>
/// Files bound to FigCraft's tabs (decision 7, Benoit's Q1 = A): FigCraft 0.18.1 hands every Save
/// over under the opened file's name, whichever tab is in front; the window decides by the tab.
/// </summary>
public sealed class FigTabsTests
{
    private const string Poster = "/home/neo/Designs/Poster.figcraft";

    private static FigTab Tab(long id, string name, bool active = false, bool dirty = false) => new(id, name, dirty, active);

    // A window that opened Poster.figcraft in tab 7, with tab 8 (a new design) next to it.
    private static FigTabs WithPosterAndNewTab(long front)
    {
        FigTabs tabs = new();
        tabs.Update([Tab(7, "Poster", active: front == 7), Tab(8, "Untitled", active: front == 8)]);
        tabs.Opened(7, Poster);
        return tabs;
    }

    [Fact]
    public void A_save_of_the_opened_tab_rewrites_its_file()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);

        FigSaveDecision decision = tabs.Decide("Poster.figcraft", saveAs: false);

        Assert.Equal(SaveAction.Rewrite, decision.Action);
        Assert.Equal(Poster, decision.Path);
        Assert.Equal(7, decision.TabId);
    }

    [Fact]
    public void A_save_of_another_tab_under_the_opened_file_s_name_never_rewrites_that_file()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 8);

        FigSaveDecision decision = tabs.Decide("Poster.figcraft", saveAs: false);

        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Null(decision.Path);
        Assert.Equal(8, decision.TabId);
        Assert.Equal("Untitled.figcraft", decision.Name);
    }

    [Fact]
    public void Once_saved_a_new_tab_has_its_own_file_and_takes_its_name()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 8);
        FigSaveDecision first = tabs.Decide("Poster.figcraft", saveAs: false);

        string? rename = tabs.Saved(first, "/home/neo/Designs/Card.figcraft");
        tabs.Renamed(8, rename!);
        FigSaveDecision next = tabs.Decide("Poster.figcraft", saveAs: false);

        Assert.Equal("Card", rename);
        Assert.Equal(SaveAction.Rewrite, next.Action);
        Assert.Equal("/home/neo/Designs/Card.figcraft", next.Path);
        Assert.Equal(Poster, tabs.FileOf(7));
    }

    [Fact]
    public void Saving_to_a_file_of_the_tab_s_own_name_needs_no_rename()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 8);

        Assert.Null(tabs.Saved(tabs.Decide("Poster.figcraft", saveAs: false), "/home/neo/Untitled.figcraft"));
    }

    [Fact]
    public void Save_as_always_asks_even_for_a_tab_with_a_file()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);

        FigSaveDecision decision = tabs.Decide("Poster.figcraft", saveAs: true);

        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Equal("Poster.figcraft", decision.Name);
        Assert.Equal("/home/neo/Designs", decision.Folder);
    }

    [Fact]
    public void A_fig_file_is_never_rewritten_with_figcraft_bytes()
    {
        FigTabs tabs = new();
        tabs.Update([Tab(3, "Landing", active: true)]);
        tabs.Opened(3, "/home/neo/Imports/Landing page.fig");

        FigSaveDecision decision = tabs.Decide("Other.figcraft", saveAs: false);

        Assert.Null(tabs.FileOf(3));
        Assert.Equal(SaveAction.AskAndBind, decision.Action);
        Assert.Equal("Landing page.figcraft", decision.Name);
        Assert.Equal("/home/neo/Imports", decision.Folder);
    }

    [Fact]
    public void A_json_design_keeps_its_format()
    {
        FigTabs tabs = new();
        tabs.Update([Tab(1, "design", active: true)]);
        tabs.Opened(1, "/home/neo/design.json");

        FigSaveDecision decision = tabs.Decide("design.json", saveAs: false);

        Assert.Equal(SaveAction.Rewrite, decision.Action);
        Assert.Equal("/home/neo/design.json", decision.Path);
    }

    [Theory]
    [InlineData("Poster.png")]
    [InlineData("Poster.pdf")]
    [InlineData("Poster.fig")]
    [InlineData("Poster.variables.json")]
    public void Exports_are_copies_next_to_the_tab_s_file(string name)
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);

        FigSaveDecision decision = tabs.Decide(name, saveAs: false);
        string? rename = tabs.Saved(decision, "/home/neo/Designs/" + name);

        Assert.Equal(SaveAction.AskForCopy, decision.Action);
        Assert.Equal(name, decision.Name);
        Assert.Equal("/home/neo/Designs", decision.Folder);
        Assert.Null(rename);
        Assert.Equal(Poster, tabs.FileOf(7));
    }

    [Fact]
    public void A_failed_save_keeps_the_window_modified_and_asks_again()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);

        tabs.SaveFailed(tabs.Decide("Poster.figcraft", saveAs: false));

        Assert.True(tabs.IsDirty);
        Assert.Equal(SaveAction.AskAndBind, tabs.Decide("Poster.figcraft", saveAs: false).Action);
        Assert.Equal([7L], tabs.WithUnsavedWork().Select(tab => tab.Id));
    }

    [Fact]
    public void A_failed_copy_changes_nothing()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);

        tabs.SaveFailed(tabs.Decide("Poster.png", saveAs: false));

        Assert.False(tabs.IsDirty);
    }

    [Fact]
    public void A_closed_tab_drops_its_file()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 8);

        tabs.Update([Tab(8, "Untitled", active: true)]);

        Assert.Null(tabs.FileOf(7));
    }

    [Fact]
    public void The_close_guard_lists_modified_tabs()
    {
        FigTabs tabs = new();
        tabs.Update([Tab(1, "A", dirty: true), Tab(2, "B", active: true), Tab(3, "C", dirty: true)]);

        Assert.Equal(["A", "C"], tabs.WithUnsavedWork().Select(tab => tab.Name));
    }

    [Fact]
    public void A_new_window_is_an_untouched_blank_until_something_happens()
    {
        FigTabs tabs = new();
        tabs.Update([Tab(1, "Untitled", active: true)]);
        tabs.StartedBlank(1);
        Assert.True(tabs.IsUntouchedBlank);
        Assert.Equal(1, tabs.BlankId);

        tabs.SetFrameDirty(true);

        Assert.False(tabs.IsUntouchedBlank);
        Assert.Null(tabs.BlankId);
    }

    [Fact]
    public void A_second_tab_or_an_open_file_is_not_blank_any_more()
    {
        FigTabs twoTabs = new();
        twoTabs.Update([Tab(1, "Untitled"), Tab(2, "Untitled 2", active: true)]);
        twoTabs.StartedBlank(1);
        Assert.False(twoTabs.IsUntouchedBlank);

        FigTabs opened = new();
        opened.Update([Tab(1, "Poster", active: true)]);
        opened.Opened(1, Poster);
        Assert.False(opened.IsUntouchedBlank);
    }

    [Fact]
    public void The_title_names_the_front_tab_s_file_and_shows_unsaved_work()
    {
        FigTabs tabs = WithPosterAndNewTab(front: 7);
        Assert.Equal("Poster.figcraft — Kazo Fig", tabs.Title("Kazo Fig", "Untitled"));

        tabs.Update([Tab(7, "Poster"), Tab(8, "Card", active: true)]);
        tabs.SetFrameDirty(true);
        Assert.Equal("*Card — Kazo Fig", tabs.Title("Kazo Fig", "Untitled"));
        Assert.Equal("Kazo Fig", tabs.Title("Kazo Fig", "Untitled", hasDocument: false));
        Assert.Equal("Sans titre — Kazo Fig", new FigTabs().Title("Kazo Fig", "Sans titre"));
    }

    [Fact]
    public void Doc_list_is_read_by_id_and_malformed_entries_are_skipped()
    {
        using JsonDocument answer = JsonDocument.Parse("""
            [
              {"index": 0, "id": 11, "name": "Poster", "dirty": false, "active": false, "path": null},
              {"index": 1, "id": 12, "name": "Card", "dirty": true, "active": true},
              {"index": 2, "name": "no id"},
              {"index": 3, "id": "13", "name": "id as text"},
              {"index": 4, "id": 11, "name": "duplicate"},
              42
            ]
            """);

        IReadOnlyList<FigTab> tabs = FigTab.ListFrom(answer.RootElement);

        Assert.Equal([new FigTab(11, "Poster", false, false), new FigTab(12, "Card", true, true)], tabs);
    }

    [Fact]
    public void An_answer_that_is_not_a_list_has_no_tab()
    {
        using JsonDocument answer = JsonDocument.Parse("""{"documents": []}""");

        Assert.Empty(FigTab.ListFrom(answer.RootElement));
    }
}
