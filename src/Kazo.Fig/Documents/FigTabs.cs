using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Documents;

/// <summary>The decision for one file FigCraft saves (<see cref="FigTabs.Decide"/>).</summary>
/// <param name="Action">What to do: rewrite the tab's file, ask for a new one, or ask for a copy's.</param>
/// <param name="TabId">The tab the save belongs to (the one in front); <see langword="null"/> when FigCraft listed none.</param>
/// <param name="TabName">The design's name on that tab when the save arrived.</param>
/// <param name="Name">The file name the save dialog suggests (or the file rewritten's name).</param>
/// <param name="Path">The file to write again, for <see cref="SaveAction.Rewrite"/>.</param>
/// <param name="Folder">The folder the save dialog opens in; <see langword="null"/> for the user's default.</param>
public sealed record FigSaveDecision(SaveAction Action, long? TabId, string TabName, string Name, string? Path, string? Folder)
{
    /// <summary>Gets whether the user picks the file in the OS save dialog.</summary>
    public bool AsksForPath => Action != SaveAction.Rewrite;

    /// <summary>Gets whether the outcome changes the design's saved state (a copy does not).</summary>
    public bool SavesTheDesign => Action != SaveAction.AskForCopy;
}

/// <summary>
/// The designs of one Kazo Fig window and their files, bound by FigCraft's tab id
/// (doc/en/plan.md, decision 7, Benoit's Q1 = A).
/// </summary>
/// <remarks>
/// <para>
/// FigCraft 0.18.1 keeps one "host file" per page, not per tab: a File › Save of any tab is handed
/// over under the name of the file the window opened, with <c>saveAs: false</c>. Trusting that name
/// would write another tab's design over the opened file. The window therefore binds each file to
/// the tab it belongs to (the <c>id</c> of <c>doc.list</c>), and decides every save by the tab in
/// front: its own file when it has one, else the save dialog. A save FigCraft marks
/// <c>saveAs: true</c> (Save as…, or a design FigCraft has no file for) always asks.
/// </para>
/// <para>
/// FigCraft marks a design saved as soon as it hands the bytes over. A tab whose last write did
/// not reach a file is remembered here: the window stays "modified" and the next save of that tab
/// asks again instead of trusting its file.
/// </para>
/// </remarks>
public sealed class FigTabs
{
    private readonly Dictionary<long, string> _files = [];
    private readonly Dictionary<long, string> _sources = [];
    private readonly HashSet<long> _unsaved = [];
    private IReadOnlyList<FigTab> _tabs = [];
    private long? _blankId;

    /// <summary>Gets the tabs as FigCraft last listed them.</summary>
    public IReadOnlyList<FigTab> Tabs => _tabs;

    /// <summary>Gets the tab in front, or <see langword="null"/> before FigCraft listed one.</summary>
    public FigTab? Active => _tabs.FirstOrDefault(tab => tab.Active) ?? _tabs.FirstOrDefault();

    /// <summary>Gets whether FigCraft reports unsaved changes in any tab.</summary>
    public bool FrameDirty { get; private set; }

    /// <summary>Gets whether the window has work that is not in a file.</summary>
    public bool IsDirty => FrameDirty || _unsaved.Count > 0;

    /// <summary>Gets whether anything happened in the window (a change, an open, a save).</summary>
    public bool Touched { get; private set; }

    /// <summary>
    /// Gets whether the window still holds only the blank design it starts with, never changed: a
    /// file opened from it replaces that design instead of opening another window.
    /// </summary>
    public bool IsUntouchedBlank =>
        !Touched && !FrameDirty && _files.Count == 0 && _sources.Count == 0 && _unsaved.Count == 0
        && _tabs.Count <= 1 && (_tabs.Count == 0 || (_tabs[0].Id == _blankId && !_tabs[0].Dirty));

    /// <summary>Gets the blank design's tab while the window still holds it untouched; else <see langword="null"/>.</summary>
    public long? BlankId => IsUntouchedBlank ? _blankId : null;

    /// <summary>Gets the file a tab's design is saved to (a <c>.figcraft</c> or <c>.json</c> file).</summary>
    /// <param name="id">FigCraft's tab id.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    public string? FileOf(long id) => _files.GetValueOrDefault(id);

    /// <summary>
    /// Gets the file a tab came from: its own file, or the file it was opened from when FigCraft
    /// cannot save in that format (<c>.fig</c>, <c>.sketch</c>, an image).
    /// </summary>
    /// <param name="id">FigCraft's tab id.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    public string? SourceOf(long id) => _files.GetValueOrDefault(id) ?? _sources.GetValueOrDefault(id);

    /// <summary>Takes FigCraft's tab list (<c>doc.list</c>): closed tabs drop their files.</summary>
    /// <param name="tabs">The tabs FigCraft has open.</param>
    public void Update(IReadOnlyList<FigTab> tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);
        _tabs = tabs;
        HashSet<long> open = [.. tabs.Select(tab => tab.Id)];
        foreach (long closed in _files.Keys.Concat(_sources.Keys).Concat(_unsaved).Where(id => !open.Contains(id)).ToList())
        {
            _files.Remove(closed);
            _sources.Remove(closed);
            _unsaved.Remove(closed);
        }

        if (_blankId is { } blank && !open.Contains(blank))
        {
            _blankId = null;
        }
    }

    /// <summary>The window opened the blank design a new window starts with (<c>file.new</c>).</summary>
    /// <param name="id">Its tab id.</param>
    public void StartedBlank(long id) => _blankId = id;

    /// <summary>
    /// A file opened in a tab: a design file (<c>.figcraft</c>, <c>.json</c>) becomes the tab's file;
    /// any other format is only where the tab came from, and its first save asks for a file.
    /// </summary>
    /// <param name="id">The tab FigCraft opened the file in.</param>
    /// <param name="path">The file's absolute path.</param>
    public void Opened(long id, string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (FigFormats.IsDesignSave(path))
        {
            _files[id] = path;
        }
        else
        {
            _sources[id] = path;
        }

        _unsaved.Remove(id);
        Touched = true;
    }

    /// <summary>FigCraft reported that unsaved changes appeared, or went away, in its tabs.</summary>
    /// <param name="dirty">The new state.</param>
    public void SetFrameDirty(bool dirty)
    {
        FrameDirty = dirty;
        Touched |= dirty;
    }

    /// <summary>Decides what to do with a file FigCraft saves, for the tab in front.</summary>
    /// <param name="name">The file name FigCraft gave.</param>
    /// <param name="saveAs">FigCraft's <c>saveAs</c>, or the window's own Save As (Ctrl+Shift+S).</param>
    /// <returns>The decision.</returns>
    public FigSaveDecision Decide(string name, bool saveAs)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        FigTab? tab = Active;
        string tabName = tab?.Name ?? string.Empty;
        string? source = tab is null ? null : SourceOf(tab.Id);
        string? folder = source is null ? null : CraftDocumentFormats.FolderOf(source);
        if (!FigFormats.IsDesignSave(name))
        {
            return new FigSaveDecision(SaveAction.AskForCopy, tab?.Id, tabName, name, null, folder);
        }

        if (!saveAs && tab is not null && FileOf(tab.Id) is { } file && !_unsaved.Contains(tab.Id))
        {
            return new FigSaveDecision(SaveAction.Rewrite, tab.Id, tabName, CraftDocumentFormats.FileNameOf(file), file, folder);
        }

        // The tab's own name, not the name FigCraft gave: on a Save, FigCraft gives the opened
        // file's name whichever tab is in front.
        string extension = CraftDocumentFormats.ExtensionOf(name);
        string stem = source is null ? tabName : Path.GetFileNameWithoutExtension(CraftDocumentFormats.FileNameOf(source));
        string suggested = saveAs && tab is null ? name : Path.ChangeExtension(FigFormats.SaveNameOf(stem), extension);
        return new FigSaveDecision(SaveAction.AskAndBind, tab?.Id, tabName, suggested, null, folder);
    }

    /// <summary>The file was written.</summary>
    /// <param name="decision">The decision carried out.</param>
    /// <param name="path">The file written.</param>
    /// <returns>
    /// The name the tab should take to match its new file (<c>Poster</c> for <c>Poster.figcraft</c>),
    /// or <see langword="null"/> when it already has it, or the save was a copy.
    /// </returns>
    public string? Saved(FigSaveDecision decision, string path)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentException.ThrowIfNullOrEmpty(path);
        Touched = true;
        if (!decision.SavesTheDesign || decision.TabId is not { } id)
        {
            return null;
        }

        _files[id] = path;
        _sources.Remove(id);
        _unsaved.Remove(id);
        string stem = Path.GetFileNameWithoutExtension(CraftDocumentFormats.FileNameOf(path));
        return stem.Length > 0 && !string.Equals(stem, decision.TabName, StringComparison.Ordinal) ? stem : null;
    }

    /// <summary>The file was not written: the dialog was cancelled, or the write failed.</summary>
    /// <param name="decision">The decision that did not complete.</param>
    public void SaveFailed(FigSaveDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        Touched = true;
        if (decision.SavesTheDesign && decision.TabId is { } id)
        {
            _unsaved.Add(id);
        }
    }

    /// <summary>The tab took the name of its new file (<c>file.rename</c>).</summary>
    /// <param name="id">The tab.</param>
    /// <param name="name">Its new name.</param>
    public void Renamed(long id, string name)
    {
        _tabs = [.. _tabs.Select(tab => tab.Id == id ? tab with { Name = name } : tab)];
    }

    /// <summary>
    /// Picks the tabs with work that is not in a file: FigCraft says they are modified, or their
    /// last save failed.
    /// </summary>
    /// <returns>The tabs to save before the window closes, in FigCraft's order.</returns>
    public IReadOnlyList<FigTab> WithUnsavedWork() => [.. _tabs.Where(tab => tab.Dirty || _unsaved.Contains(tab.Id))];

    /// <summary>
    /// Builds the window title: <c>*name — Kazo Fig</c>, the name of the front tab's file (or of
    /// the design when it has none), the asterisk while any tab has work that is not in a file.
    /// </summary>
    /// <param name="appName">The product name.</param>
    /// <param name="untitled">The name of a design with no name, in the user's language.</param>
    /// <param name="hasDocument">Whether FigCraft is running.</param>
    /// <returns>The title.</returns>
    public string Title(string appName, string untitled, bool hasDocument = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (!hasDocument)
        {
            return appName;
        }

        FigTab? tab = Active;
        string name = tab is not null && SourceOf(tab.Id) is { } path
            ? CraftDocumentFormats.FileNameOf(path)
            : tab is { Name.Length: > 0 } ? tab.Name : untitled;
        return $"{(IsDirty ? "*" : string.Empty)}{name} — {appName}";
    }
}
