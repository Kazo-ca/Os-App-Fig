using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Hosting;

/// <summary>
/// The chords the iframe gives back to the window instead of FigCraft: the OS chords, Ctrl+N for
/// a new window, and Ctrl+Shift+S for Save as (doc/en/plan.md, decision 7, Benoit's Q1 = A).
/// </summary>
/// <remarks>
/// FigCraft hands a Save as over with <c>saveAs: false</c> when the design's name is the name of the
/// file the window opened, the same as a Save. The window takes Ctrl+Shift+S, FigCraft's own Save as
/// chord, and runs Save as itself, so the shortcut always asks for a new file.
/// </remarks>
public static class FigKeys
{
    /// <summary>Gets the Ctrl+Shift+S chord (Save as).</summary>
    public static ReservedKey SaveAs { get; } = ReservedKeys.Parse("Ctrl+Shift+S", ReservedKeyTarget.App);

    /// <summary>Gets every chord the window reserves.</summary>
    public static IReadOnlyList<ReservedKey> All { get; } = [.. ReservedKeys.All, SaveAs];

    /// <summary>
    /// Reads a typed chord. The proxy reports the key as the browser gave it (<c>S</c> with Shift),
    /// so the comparison ignores the case of the key and takes the physical code when it matches.
    /// </summary>
    /// <param name="pressed">The chord the proxy reported.</param>
    /// <returns>What the window does.</returns>
    public static KeyAction ActionOf(ReservedKey pressed)
    {
        ArgumentNullException.ThrowIfNull(pressed);
        if (Matches(pressed, ReservedKeys.NewWindow))
        {
            return KeyAction.NewWindow;
        }

        return Matches(pressed, SaveAs) ? KeyAction.SaveAs : KeyAction.None;
    }

    private static bool Matches(ReservedKey pressed, ReservedKey chord)
    {
        if (pressed.Ctrl != chord.Ctrl || pressed.Alt != chord.Alt || pressed.Shift != chord.Shift || pressed.Meta != chord.Meta)
        {
            return false;
        }

        bool sameKey = pressed.Key is { } key && chord.Key is { } wanted
            && string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase);
        bool sameCode = pressed.Code is { } code && chord.Code is { } wantedCode
            && string.Equals(code, wantedCode, StringComparison.Ordinal);
        return sameKey || sameCode;
    }
}

/// <summary>What a chord typed in FigCraft asks the window for.</summary>
public enum KeyAction
{
    /// <summary>Nothing the window knows.</summary>
    None,

    /// <summary>A new, empty Kazo Fig window.</summary>
    NewWindow,

    /// <summary>Save the design in front under a new name.</summary>
    SaveAs,
}
