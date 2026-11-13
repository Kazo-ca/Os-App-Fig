using System.Text.Json;
using Kazo.Fig.Documents;
using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Microsoft.JSInterop;

namespace Kazo.Fig;

/// <summary>
/// Opening, saving, Save as and closing (doc/en/plan.md, work item 2): the window reads and writes
/// the files with <see cref="KazoFile"/>, asks the OS dialogs, keeps the title and guards the close.
/// <see cref="FigTabs"/> decides where each save goes, by FigCraft's tab (decision 7, Q1 = A).
/// </summary>
/// <remarks>
/// FigCraft hands every file it saves over to the window (<c>figcraft:save</c>). For the design it
/// gives the name of the file the window opened whichever tab is in front, so the window asks
/// FigCraft which tab is in front (<c>doc.list</c>) and writes that tab's own file, or asks for one.
/// Every save that is not <c>.figcraft</c> or <c>.json</c> is a copy (an export).
/// </remarks>
public sealed partial class FigWindow : IWindowCloseGuard
{
    /// <summary>The app's id, to start another Kazo Fig window on a file.</summary>
    private const string AppId = "ca.kazo.fig";

    /// <summary>The window's name for its app, at the end of its title.</summary>
    private const string AppName = "Kazo Fig";

    /// <summary>How long an armed Save as waits for FigCraft's save before it lapses.</summary>
    private static readonly TimeSpan SaveAsWindow = TimeSpan.FromSeconds(10);

    /// <summary>How long the window waits for FigCraft to show a file it sent, or to hand over a save it asked for.</summary>
    private static readonly TimeSpan FigCraftAnswerWindow = TimeSpan.FromSeconds(15);

    /// <summary>How often the window looks for the tab of a file it sent.</summary>
    private static readonly TimeSpan OpenPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly FigTabs _tabs = new();

    // Saves run one at a time: each may show a dialog, and the order is FigCraft's.
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    // For the close guard: completed when FigCraft hands the next save over, then with its outcome (true = written).
    private TaskCompletionSource? _nextSaveStarted;
    private TaskCompletionSource<bool>? _nextSave;

    // When the window asked FigCraft for a Save as: the next design save asks for a new file.
    private DateTimeOffset? _saveAsArmedAt;

    // A message over FigCraft: a failed write, a cancelled save, a copy written.
    private string? _notice;
    private bool _noticeIsError;

    /// <summary>Gets the title the OS shows for this window: <c>*name — Kazo Fig</c>.</summary>
    internal string DocumentTitle => _tabs.Title(AppName, L["Untitled"], _startup.State == FrameState.Running);

    /// <summary>
    /// Opens a file: in this window when it holds only an untouched blank design, otherwise in a new
    /// Kazo Fig window (a file opened from the OS never joins a window that has work).
    /// </summary>
    /// <param name="path">The file's absolute path.</param>
    /// <returns><see langword="true"/> when the file opened in this window.</returns>
    internal async Task<bool> OpenInNewWindowAsync(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        if (_frame is { } frame && _startup.State == FrameState.Running)
        {
            await RefreshTabsAsync(frame);
            if (_tabs.IsUntouchedBlank)
            {
                return await OpenFileHereAsync(path);
            }
        }

        try
        {
            await KazoApps.LaunchAsync(AppId, [path], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or KazoUnauthorizedAccessException)
        {
            await ShowNoticeAsync(L["OpenFailed", CraftDocumentFormats.FileNameOf(path), exception.Message], error: true);
        }

        return false;
    }

    /// <summary>
    /// Asks before closing a window with work that is not in a file: FigCraft lists its tabs
    /// (<c>doc.list</c>); "Yes" shows and saves each modified one, and closes only once every write
    /// succeeded.
    /// </summary>
    /// <param name="cancellationToken">Cancels the close request.</param>
    /// <returns><see langword="true"/> to close.</returns>
    public async ValueTask<bool> ConfirmCloseAsync(CancellationToken cancellationToken = default)
    {
        if (_frame is not { } frame || _startup.State != FrameState.Running)
        {
            return true;
        }

        // A save still being written must end first: FigCraft already counts the design as saved
        // once it handed the bytes over, so closing now would lose the write.
        await _saveGate.WaitAsync(cancellationToken);
        _saveGate.Release();

        if (!await RefreshTabsAsync(frame, cancellationToken))
        {
            // FigCraft does not answer, so it cannot save either: closing without saving is the
            // only choice left, and it is the user's.
            if (!_tabs.IsDirty)
            {
                return true;
            }

            MessageBoxDialogResult discard = await MessageBox(
                L["CloseTitle"], L["CloseUnanswered", _tabs.Active?.Name ?? L["Untitled"]], MessageBoxType.YesNo, cancellationToken);
            return discard.Result == MessageBoxResult.Yes;
        }

        IReadOnlyList<FigTab> unsaved = _tabs.WithUnsavedWork();
        if (unsaved.Count == 0)
        {
            return true;
        }

        string names = string.Join(", ", unsaved.Select(tab => tab.Name.Length > 0 ? tab.Name : L["Untitled"].Value));
        MessageBoxDialogResult answer = await MessageBox(
            L["CloseTitle"], L["CloseUnsaved", names], MessageBoxType.YesNoCancel, cancellationToken);
        return answer.Result switch
        {
            MessageBoxResult.Yes => await SaveAllAsync(frame, unsaved, cancellationToken),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    // After `ready`: the file the window was started for, else a blank design. In host mode
    // FigCraft starts on its home screen with no design.
    private async Task PrepareDocumentAsync(CraftFrame frame)
    {
        // Runs after `ready`, a [JSInvokable] callback: the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        bool opened = _request.Path is { } path && await OpenFileHereAsync(path);
        if (!opened)
        {
            await ExecuteAsync(frame, "file.new", new { });
            if (await RefreshTabsAsync(frame) && _tabs.Active is { } blank)
            {
                _tabs.StartedBlank(blank.Id);
            }
        }

        await frame.RequestAsync("ui.set", new { home = false }, cancellationToken: AppContext.CancellationToken);
        await UpdateTitleAsync();
    }

    // Reads a file and sends it to FigCraft, then binds it to the tab FigCraft opens it in. A file
    // that does not read, or that FigCraft does not show, leaves a message.
    private async Task<bool> OpenFileHereAsync(string path)
    {
        if (_frame is not { } frame)
        {
            return false;
        }

        string name = CraftDocumentFormats.FileNameOf(path);
        byte[] bytes;
        try
        {
            bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            await ShowNoticeAsync(L["OpenFailed", name, exception.Message], error: true);
            return false;
        }

        await RefreshTabsAsync(frame);
        long? blank = _tabs.BlankId;
        HashSet<long> before = [.. _tabs.Tabs.Select(tab => tab.Id)];
        if (!await frame.OpenAsync(name, bytes))
        {
            await ShowNoticeAsync(L["OpenFailed", name, L["NotConnected"]], error: true);
            return false;
        }

        // FigCraft opens the file on its next frames ("Opening as a new file"): its new tab is the file's.
        if (await WaitForNewTabAsync(frame, before) is not { } opened)
        {
            await ShowNoticeAsync(L["OpenFailed", name, L["NotOpened"]], error: true);
            return false;
        }

        _tabs.Opened(opened.Id, path);
        if (blank is { } untouched)
        {
            // The untouched blank design the window started with gives way to the file.
            await frame.RequestAsync(
                "engine.execute",
                new { command = "file.close", @params = new { }, document = untouched },
                cancellationToken: AppContext.CancellationToken);
            await RefreshTabsAsync(frame);
        }

        await frame.RequestAsync("ui.set", new { home = false }, cancellationToken: AppContext.CancellationToken);
        await ClearNoticeAsync();
        await UpdateTitleAsync();
        return true;
    }

    private async Task<FigTab?> WaitForNewTabAsync(CraftFrame frame, HashSet<long> before)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + FigCraftAnswerWindow;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await RefreshTabsAsync(frame) && _tabs.Tabs.FirstOrDefault(tab => !before.Contains(tab.Id)) is { } opened)
            {
                return opened;
            }

            await Task.Delay(OpenPollInterval, AppContext.CancellationToken);
        }

        return null;
    }

    // Asks FigCraft for its tabs (doc.list); false when it does not answer.
    private async Task<bool> RefreshTabsAsync(CraftFrame frame, CancellationToken cancellationToken = default)
    {
        FrameCommandResult list = await frame.RequestAsync(
            "engine.execute",
            new { command = "doc.list", @params = new { } },
            cancellationToken: cancellationToken == default ? AppContext.CancellationToken : cancellationToken);
        if (!list.Ok || list.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        _tabs.Update(FigTab.ListFrom(list.Value));
        return true;
    }

    private async Task OnDirtyChangedAsync(bool dirty)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        _tabs.SetFrameDirty(dirty);
        if (_frame is { } frame)
        {
            // The tab in front may have changed too: the title names it.
            await RefreshTabsAsync(frame);
        }

        await UpdateTitleAsync();
    }

    /// <summary>
    /// Save as (Ctrl+Shift+S): the next design FigCraft saves asks for a new file, then FigCraft
    /// runs its own File › Save as on the tab in front.
    /// </summary>
    /// <returns>A task that completes once FigCraft took the command.</returns>
    private async Task SaveAsAsync()
    {
        if (_frame is not { } frame || _startup.State != FrameState.Running)
        {
            return;
        }

        _saveAsArmedAt = DateTimeOffset.UtcNow;
        FrameCommandResult started = await frame.RequestAsync(
            "ui.menu.invoke", new { id = "file.saveAs" }, cancellationToken: AppContext.CancellationToken);
        if (!started.Ok)
        {
            _saveAsArmedAt = null;
            await ShowNoticeAsync(L["SaveFailed", _tabs.Active?.Name ?? L["Untitled"], started.Error ?? string.Empty], error: true);
        }
    }

    // One file FigCraft saves: decided here and by FigTabs, written here.
    private async Task<bool> OnSavingAsync(FrameSave save)
    {
        // Called from JavaScript ([JSInvokable]): the app's scope must be entered for KazoFile.
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        Interlocked.Exchange(ref _nextSaveStarted, null)?.TrySetResult();
        await _saveGate.WaitAsync(AppContext.CancellationToken);
        bool written = false;
        try
        {
            written = await SaveAsync(save);
        }
        finally
        {
            _saveGate.Release();
            Interlocked.Exchange(ref _nextSave, null)?.TrySetResult(written);
            await UpdateTitleAsync();
        }

        return written;
    }

    private async Task<bool> SaveAsync(FrameSave save)
    {
        if (string.IsNullOrEmpty(save.Name) || _frame is not { } frame)
        {
            return false;
        }

        // The save is the tab's in front: FigCraft hands it over from that tab.
        await RefreshTabsAsync(frame);
        bool armed = _saveAsArmedAt is { } at && DateTimeOffset.UtcNow - at <= SaveAsWindow;
        bool design = FigFormats.IsDesignSave(save.Name);
        if (design)
        {
            _saveAsArmedAt = null;
        }

        FigSaveDecision decision = _tabs.Decide(save.Name, save.SaveAs || (design && armed));
        try
        {
            return await CarryOutAsync(frame, decision, save.Bytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Whatever went wrong (a dialog, the file system), the design must not pass for saved.
            _tabs.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", save.Name, exception.Message], error: true);
            return false;
        }
    }

    // Asks for the path when the decision says so, writes the bytes, and records the outcome.
    private async Task<bool> CarryOutAsync(CraftFrame frame, FigSaveDecision decision, byte[] bytes)
    {
        string? path = decision.AsksForPath ? await AskSavePathAsync(decision) : decision.Path;
        if (path is null)
        {
            _tabs.SaveFailed(decision);
            if (decision.SavesTheDesign)
            {
                await ShowNoticeAsync(L["SaveCancelled", decision.Name], error: false);
            }

            return false;
        }

        try
        {
            await KazoFile.WriteAllBytesAsync(path, bytes, AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _tabs.SaveFailed(decision);
            await ShowNoticeAsync(L["SaveFailed", CraftDocumentFormats.FileNameOf(path), exception.Message], error: true);
            return false;
        }

        if (!decision.SavesTheDesign)
        {
            _tabs.Saved(decision, path);
            await ShowNoticeAsync(L["CopySaved", CraftDocumentFormats.FileNameOf(path)], error: false);
            return true;
        }

        if (_tabs.Saved(decision, path) is { } newName && decision.TabId is { } tab)
        {
            await RenameTabAsync(frame, tab, newName);
        }

        await ClearNoticeAsync();
        return true;
    }

    // The tab takes its new file's name, as a desktop editor's tab does after Save as. The rename
    // is a change for FigCraft: the design was just saved, so it is marked saved again.
    private async Task RenameTabAsync(CraftFrame frame, long tab, string name)
    {
        FrameCommandResult renamed = await frame.RequestAsync(
            "engine.execute",
            new { command = "file.rename", @params = new { name }, document = tab },
            cancellationToken: AppContext.CancellationToken);
        if (!renamed.Ok)
        {
            return;
        }

        _tabs.Renamed(tab, name);
        await frame.RequestAsync(
            "engine.execute",
            new { command = "file.markSaved", @params = new { }, document = tab },
            cancellationToken: AppContext.CancellationToken);
    }

    // The OS save dialog, in the design's folder, for the format FigCraft chose.
    private async Task<string?> AskSavePathAsync(FigSaveDecision decision)
    {
        string extension = CraftDocumentFormats.ExtensionOf(decision.Name);
        CraftDocumentFormat? format = FigFormats.Formats.Find(decision.Name);
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = decision.Action == SaveAction.AskForCopy ? L["SaveCopyTitle"] : L["SaveTitle"],
            InitialDirectory = DialogFolder(decision.Folder),
            SuggestedFileName = decision.Name,
            DefaultExtension = extension.Length > 0 ? extension : null,
            Filters = extension.Length > 0
                ? [new FileDialogFilter(L["FilterFormat", extension], [extension], format is null ? [] : [format.MediaType])]
                : [],
            ConfirmOverwrite = true,
        });
        return result.Status == FileDialogStatus.Selected && result.Resource is { } chosen
            ? CraftDocumentFormats.WithExtensionOf(chosen.Path.Value, decision.Name)
            : null;
    }

    // "Yes" in the close dialog: each modified tab is brought in front and saved in turn (FigCraft's
    // own File › Save); the window closes only when all of them reached a file.
    private async Task<bool> SaveAllAsync(CraftFrame frame, IReadOnlyList<FigTab> tabs, CancellationToken cancellationToken)
    {
        foreach (FigTab tab in tabs)
        {
            if (!await RefreshTabsAsync(frame, cancellationToken))
            {
                return false;
            }

            int index = _tabs.Tabs.ToList().FindIndex(open => open.Id == tab.Id);
            if (index < 0)
            {
                // Closed meanwhile: nothing left to save.
                continue;
            }

            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> outcome = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _nextSaveStarted = started;
            _nextSave = outcome;
            FrameCommandResult result = await frame.RequestAsync(
                "engine.execute",
                new { command = "doc.switch", @params = new { index } },
                cancellationToken: cancellationToken);
            if (result.Ok)
            {
                result = await frame.RequestAsync("ui.menu.invoke", new { id = "file.save" }, cancellationToken: cancellationToken);
            }

            if (!result.Ok || !await HandedOverAsync(started.Task, cancellationToken))
            {
                _nextSaveStarted = null;
                _nextSave = null;
                await ShowNoticeAsync(L["SaveFailed", tab.Name, result.Error ?? L["NotSaved"]], error: true);
                return false;
            }

            if (!await outcome.Task.WaitAsync(cancellationToken))
            {
                return false;
            }
        }

        return true;
    }

    // Whether FigCraft handed the save over in time (the dialog that may follow takes its own time).
    private static async Task<bool> HandedOverAsync(Task started, CancellationToken cancellationToken)
    {
        try
        {
            await started.WaitAsync(FigCraftAnswerWindow, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    private VirtualPath DialogFolder(string? folder) => VirtualPath.Parse(folder ?? AppContext.HomeDirectory);

    private async Task UpdateTitleAsync()
    {
        try
        {
            await SetWindowTitleAsync(DocumentTitle);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JSDisconnectedException)
        {
            // The window is closing.
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ShowNoticeAsync(string text, bool error)
    {
        _notice = text;
        _noticeIsError = error;
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearNoticeAsync()
    {
        if (_notice is not null)
        {
            _notice = null;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void DismissNotice() => _notice = null;
}
