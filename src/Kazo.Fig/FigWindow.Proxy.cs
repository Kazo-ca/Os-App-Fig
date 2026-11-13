using Kazo.Os.App.Abstractions;
using Kazo.Os.AppSdk;
using Kazo.Os.AppSdk.Apps;
using Kazo.Os.AppSdk.Blazor;
using Kazo.Os.AppSdk.Craft;
using Kazo.Os.AppSdk.IO;
using Kazo.Fig.Documents;
using Kazo.Fig.Hosting;

namespace Kazo.Fig;

/// <summary>
/// What the window does with the module's proxy (doc/en/plan.md, work item 2): FigCraft's file
/// picker, the chords the window keeps, the user's FigCraft storage, and the OS theme and language.
/// </summary>
/// <remarks>
/// The proxy's messages arrive through a <c>[JSInvokable]</c> callback, outside the scope the OS opens
/// for app code: each handler that reaches the SDK (files, launching) enters the app's context itself.
/// </remarks>
public sealed partial class FigWindow
{
    /// <summary>
    /// How long FigCraft's storage writes are gathered before the file is written: counted from
    /// the first unsaved write, so a FigCraft that keeps writing still reaches the file.
    /// </summary>
    public static readonly TimeSpan SettingsWriteDelay = TimeSpan.FromMilliseconds(500);

    private WebStorageFile? _settingsFile;
    private CancellationTokenSource? _settingsWrite;

    // Reads the user's FigCraft storage before the iframe starts: FigCraft reads it as it starts.
    // It holds figcraft.userName and, after FigCraft stopped on an error, figcraft.recovery (the
    // designs it could not save, which the next window opens again). It lives in
    // ~/.config/ca.kazo.fig/, so each Kazo user has their own instead of the browser's shared one.
    private async Task<IReadOnlyDictionary<string, string>?> LoadSettingsAsync()
    {
        _settingsFile ??= new WebStorageFile(AppContext.HomeDirectory, AppId);
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            return new Dictionary<string, string>(await _settingsFile.LoadAsync(AppContext.CancellationToken), StringComparer.Ordinal);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // The proxy's events of one frame (subscribed in StartFrameAsync, removed in StopFrameAsync).
    private void ConnectProxy(CraftFrame frame, bool connect)
    {
        if (connect)
        {
            frame.KeyPressed += OnKeyPressedAsync;
            frame.PickRequested += OnPickRequestedAsync;
            frame.SettingChanged += OnSettingChangedAsync;
            frame.SchemeChanged += OnSchemeChangedAsync;
        }
        else
        {
            frame.KeyPressed -= OnKeyPressedAsync;
            frame.PickRequested -= OnPickRequestedAsync;
            frame.SettingChanged -= OnSettingChangedAsync;
            frame.SchemeChanged -= OnSchemeChangedAsync;
        }
    }

    // FigCraft is listening: it takes the OS theme (ui.set theme) and the user's language
    // (pref.language; FigCraft resolves the culture name itself). A refused command leaves FigCraft
    // as it was: the design matters more.
    private async Task ApplyThemeAndLanguageAsync(CraftFrame frame)
    {
        await OnSchemeChangedAsync(await frame.GetColorSchemeAsync());
        await ExecuteAsync(frame, "pref.language", new { language = FigCraftLanguage.Of(AppContext.Culture) });
    }

    // FigCraft follows the OS theme: "light" or "dark" (FigCraft's own "system" would follow the
    // browser, not Kazo OS).
    private async Task OnSchemeChangedAsync(string scheme)
    {
        if (_frame is { } frame)
        {
            await frame.RequestAsync("ui.set", new { theme = FigCraftTheme.Of(scheme) }, cancellationToken: AppContext.CancellationToken);
        }
    }

    // Ctrl+N: a new Kazo Fig window (FigCraft's File › New design opens a tab in this one).
    // Ctrl+Shift+S: Save as, always to a new file (decision 7).
    private Task OnKeyPressedAsync(ReservedKey key) => FigKeys.ActionOf(key) switch
    {
        KeyAction.NewWindow => InvokeAsync(LaunchNewWindowAsync),
        KeyAction.SaveAs => InvokeAsync(SaveAsAsync),
        _ => Task.CompletedTask,
    };

    // A new, empty Kazo Fig window.
    private async Task LaunchNewWindowAsync()
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await KazoApps.LaunchAsync(AppId, [], AppContext.CancellationToken);
        }
        catch (Exception exception) when (exception is KazoAppLaunchException or UnauthorizedAccessException)
        {
            await ShowProblemAsync(L["NewWindowFailed", exception.Message]);
        }
    }

    // FigCraft opened its file picker: the OS dialog instead. File › Open (designs) is not handed
    // to FigCraft: it hears "cancelled", and the file opens through OpenInNewWindowAsync (here only
    // for an untouched blank design), so every design of a window has its file. Import, Place image
    // and an image fill get the file read and handed over.
    private Task OnPickRequestedAsync(PickRequest request) => InvokeAsync(async () =>
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CraftFrame? frame = _frame;
        if (frame is null)
        {
            return;
        }

        bool design = FigFormats.IsOpenPicker(request);
        IReadOnlyList<PickedFile>? files = null;
        string? document = null;
        try
        {
            OpenFileDialogResult choice = await OpenFileAsync(
                new OpenFileDialogRequest
                {
                    Title = L[design ? "PickDesignTitle" : "PickFileTitle"],
                    InitialDirectory = VirtualPath.Parse(AppContext.HomeDirectory),
                    AllowMultiple = request.Multiple && !design,
                    Filters = FiltersOf(request, design),
                },
                AppContext.CancellationToken);
            if (choice.Status == FileDialogStatus.Selected && choice.Resources.Count > 0)
            {
                if (design)
                {
                    document = choice.Resources[0].Path.Value;
                }
                else
                {
                    files = await ReadPickedAsync(choice.Resources);
                }
            }
        }
        finally
        {
            // FigCraft waits on its picker until it hears back: it always does, even on a failure.
            await frame.AnswerPickAsync(request.Id, files);
        }

        // After the answer: FigCraft's picker is closed before a design may open in this window.
        if (document is not null)
        {
            await OpenInNewWindowAsync(document);
        }
    });

    private async Task<IReadOnlyList<PickedFile>?> ReadPickedAsync(IReadOnlyList<SelectedFileResource> resources)
    {
        List<PickedFile> files = [];
        foreach (SelectedFileResource resource in resources)
        {
            string path = resource.Path.Value;
            string name = KazoPath.GetFileName(path) ?? path;
            try
            {
                byte[] bytes = await KazoFile.ReadAllBytesAsync(path, AppContext.CancellationToken);
                files.Add(new PickedFile(name, string.Empty, bytes));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                await ShowProblemAsync(L["ReadFailed", name, exception.Message]);
                return null;
            }
        }

        return files;
    }

    // The extensions FigCraft accepts, then every file (FigCraft checks what it gets).
    private List<FileDialogFilter> FiltersOf(PickRequest request, bool design)
    {
        List<FileDialogFilter> filters = [];
        if (request.Extensions.Count > 0)
        {
            filters.Add(new FileDialogFilter(L[design ? "DesignsFilter" : "FilesFilter"], request.Extensions, []));
        }

        filters.Add(new FileDialogFilter(L["AllFiles"], [], []));
        return filters;
    }

    // FigCraft wrote to its storage: the user's file is written a moment later, with every write
    // gathered until then. A write already waiting takes the new value: it saves the file as it
    // is when it runs.
    private Task OnSettingChangedAsync(string key, string? value)
    {
        if (_settingsFile is null || !_settingsFile.Set(key, value) || _settingsWrite is not null)
        {
            return Task.CompletedTask;
        }

        CancellationTokenSource write = CancellationTokenSource.CreateLinkedTokenSource(AppContext.CancellationToken);
        _settingsWrite = write;
        _ = WriteSettingsLaterAsync(write);
        return Task.CompletedTask;
    }

    private async Task WriteSettingsLaterAsync(CancellationTokenSource write)
    {
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        CancellationToken cancellationToken = write.Token;
        try
        {
            await Task.Delay(SettingsWriteDelay, cancellationToken);
            // From here a new write schedules its own save, since this one may already have read the values.
            if (ReferenceEquals(_settingsWrite, write))
            {
                _settingsWrite = null;
                write.Dispose();
            }

            await _settingsFile!.SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed: FlushSettingsAsync writes what was waiting.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The values stay in this window's FigCraft; the next write tries again.
        }
    }

    // Writes what is still waiting when the window closes.
    private async Task FlushSettingsAsync()
    {
        if (_settingsWrite is null || _settingsFile is null)
        {
            return;
        }

        _settingsWrite.Cancel();
        _settingsWrite.Dispose();
        _settingsWrite = null;
        using IDisposable scope = KazoExecutionContext.Enter(AppContext);
        try
        {
            await _settingsFile.SaveAsync(CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Closing: nothing more to do.
        }
    }

    private async Task ShowProblemAsync(string message)
    {
        try
        {
            await MessageBox(L["ProblemTitle"], message, MessageBoxType.Ok, MessageBoxIcon.Error, AppContext.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }
}
