using Kazo.Fig.Hosting;
using Kazo.Os.AppSdk.Craft;

namespace Kazo.Fig.Tests;

/// <summary>The chords the window takes from FigCraft (decision 7, Q1 = A).</summary>
public sealed class FigKeysTests
{
    // What the proxy reports: the key as the browser gives it, the physical code, the modifiers.
    private static ReservedKey Typed(string key, string code, bool ctrl = false, bool shift = false) =>
        new(key, code, ctrl, Alt: false, shift, Meta: false, ReservedKeyTarget.App);

    [Fact]
    public void The_window_reserves_the_OS_chords_and_save_as()
    {
        Assert.Superset(ReservedKeys.All.ToHashSet(), FigKeys.All.ToHashSet());
        Assert.Contains(FigKeys.SaveAs, FigKeys.All);
        Assert.Equal(ReservedKeyTarget.App, FigKeys.SaveAs.Target);
    }

    [Theory]
    [InlineData("S")]
    [InlineData("s")]
    public void Ctrl_Shift_S_is_save_as_whatever_the_case_of_the_key(string key)
    {
        Assert.Equal(KeyAction.SaveAs, FigKeys.ActionOf(Typed(key, "KeyS", ctrl: true, shift: true)));
    }

    [Fact]
    public void Ctrl_Shift_S_on_another_layout_is_found_by_its_code()
    {
        Assert.Equal(KeyAction.SaveAs, FigKeys.ActionOf(Typed("Ś", "KeyS", ctrl: true, shift: true)));
    }

    [Fact]
    public void Ctrl_N_is_a_new_window()
    {
        Assert.Equal(KeyAction.NewWindow, FigKeys.ActionOf(Typed("n", "KeyN", ctrl: true)));
    }

    [Theory]
    [InlineData("s", "KeyS", true, false)]
    [InlineData("F12", "F12", false, false)]
    [InlineData("n", "KeyN", true, true)]
    [InlineData("r", "KeyR", false, false)]
    public void Other_chords_stay_with_FigCraft(string key, string code, bool ctrl, bool shift)
    {
        Assert.Equal(KeyAction.None, FigKeys.ActionOf(Typed(key, code, ctrl, shift)));
    }
}
