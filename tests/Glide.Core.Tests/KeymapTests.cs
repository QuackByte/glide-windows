namespace Glide.Core.Tests;

public class KeymapTests
{
    private static bool IsRejected(ShortcutPolicy.Verdict verdict) => verdict is ShortcutPolicy.Verdict.Rejected;
    private static bool IsWarning(ShortcutPolicy.Verdict verdict) => verdict is ShortcutPolicy.Verdict.Warning;

    [Fact]
    public void EveryActionHasADefault()
    {
        foreach (var action in GlideActions.All)
            Assert.True(Keymap.Defaults.ContainsKey(action), $"{action} has no default shortcut");
    }

    [Fact]
    public void DefaultsAreUnique()
    {
        var combos = Keymap.Defaults.Values.ToList();
        Assert.Equal(combos.Count, combos.Distinct().Count());
    }

    [Fact]
    public void DefaultsPassShortcutPolicy()
    {
        foreach (var (action, combo) in Keymap.Defaults)
            Assert.True(ShortcutPolicy.Evaluate(combo) is ShortcutPolicy.Verdict.Ok, $"{action} default {combo} is flagged");
    }

    [Fact]
    public void DefaultsAvoidTextEditingCombos()
    {
        foreach (var combo in Keymap.Defaults.Values.Where(c => KeyCode.Arrows.Contains(c.KeyCode)))
        {
            var primary = combo.Modifiers & ~KeyModifiers.Shift;
            Assert.NotEqual(KeyModifiers.Alt, primary);
            Assert.NotEqual(KeyModifiers.Control, primary);
            Assert.NotEqual(KeyModifiers.Control | KeyModifiers.Alt, primary);
        }
    }

    [Fact]
    public void DefaultDisplayNames()
    {
        Assert.Equal("Win+←", Keymap.Defaults[GlideAction.FocusColumnLeft].ToString());
        Assert.Equal("Win+Shift+→", Keymap.Defaults[GlideAction.MoveColumnRight].ToString());
        Assert.Equal("Win+[", Keymap.Defaults[GlideAction.ConsumeOrExpelLeft].ToString());
        Assert.Equal("Win+Ctrl+Shift+T", Keymap.Defaults[GlideAction.ToggleTiling].ToString());
    }

    [Fact]
    public void OverrideAndReset()
    {
        var keymap = new Keymap();
        var combo = new KeyCombo('H', KeyModifiers.Win | KeyModifiers.Alt);
        keymap.Set(combo, GlideAction.FocusColumnLeft);
        Assert.Equal(combo, keymap.Combo(GlideAction.FocusColumnLeft));
        Assert.Equal(GlideAction.FocusColumnLeft, keymap.Action(combo));
        keymap.Reset(GlideAction.FocusColumnLeft);
        Assert.Equal(Keymap.Defaults[GlideAction.FocusColumnLeft], keymap.Combo(GlideAction.FocusColumnLeft));
        Assert.Empty(keymap.Overrides);
    }

    [Fact]
    public void Unbinding()
    {
        var keymap = new Keymap();
        keymap.Set(null, GlideAction.CenterColumn);
        Assert.Null(keymap.Combo(GlideAction.CenterColumn));
        Assert.DoesNotContain(keymap.Bindings, b => b.Action == GlideAction.CenterColumn);
    }

    [Fact]
    public void BindingDuplicateDisplacesOtherAction()
    {
        var keymap = new Keymap();
        var displaced = keymap.Set(Keymap.Defaults[GlideAction.FocusColumnRight], GlideAction.FocusColumnLeft);
        Assert.Equal([GlideAction.FocusColumnRight], displaced);
        Assert.Null(keymap.Combo(GlideAction.FocusColumnRight));
        Assert.Equal(GlideAction.FocusColumnLeft, keymap.Action(Keymap.Defaults[GlideAction.FocusColumnRight]));
    }

    [Fact]
    public void ResetDisplacesActionHoldingTheDefault()
    {
        var keymap = new Keymap();
        keymap.Set(new KeyCombo('H', KeyModifiers.Win | KeyModifiers.Alt), GlideAction.FocusColumnLeft);
        keymap.Set(Keymap.Defaults[GlideAction.FocusColumnLeft], GlideAction.CenterColumn);
        keymap.Reset(GlideAction.FocusColumnLeft);
        Assert.Equal(Keymap.Defaults[GlideAction.FocusColumnLeft], keymap.Combo(GlideAction.FocusColumnLeft));
        Assert.Null(keymap.Combo(GlideAction.CenterColumn));
    }

    [Fact]
    public void StorageRoundTrip()
    {
        var keymap = new Keymap();
        keymap.Set(new KeyCombo('H', KeyModifiers.Win | KeyModifiers.Alt | KeyModifiers.Control), GlideAction.FocusColumnLeft);
        keymap.Set(null, GlideAction.ToggleTiling);
        var restored = Keymap.FromStorage(keymap.Storage);
        Assert.Equal(keymap, restored);
        Assert.Equal(new KeyCombo('H', KeyModifiers.Win | KeyModifiers.Alt | KeyModifiers.Control), restored.Combo(GlideAction.FocusColumnLeft));
        Assert.Null(restored.Combo(GlideAction.ToggleTiling));
    }

    [Fact]
    public void StorageIgnoresUnknownAndMalformedEntries()
    {
        var keymap = Keymap.FromStorage(new Dictionary<string, string> { ["notAnAction"] = "3:4", ["CenterColumn"] = "garbage", ["42"] = "8:37" });
        Assert.Equal(new Keymap(), keymap);
    }

    [Fact]
    public void ModifierStorageBitsAreStable()
    {
        // Persisted in settings files, so the bit values must never change.
        Assert.Equal("9:37", new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Win | KeyModifiers.Control).StorageValue);
        Assert.Equal("6:72", new KeyCombo('H', KeyModifiers.Alt | KeyModifiers.Shift).StorageValue);
        Assert.Equal(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Win | KeyModifiers.Control), KeyCombo.FromStorage("9:37"));
    }

    [Fact]
    public void Policy()
    {
        Assert.True(IsRejected(ShortcutPolicy.Evaluate(new KeyCombo('A', KeyModifiers.None))));
        Assert.True(IsRejected(ShortcutPolicy.Evaluate(new KeyCombo('A', KeyModifiers.Shift))));
        Assert.True(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.F1, KeyModifiers.None)) is ShortcutPolicy.Verdict.Ok); // Bare F1 is allowed
        Assert.True(IsRejected(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.L, KeyModifiers.Win))), "Win+L locks the PC");
        Assert.True(IsWarning(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Control))));
        Assert.True(IsWarning(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Control | KeyModifiers.Shift))));
        Assert.True(IsWarning(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.RightArrow, KeyModifiers.Alt))));
        Assert.True(IsWarning(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.Tab, KeyModifiers.Alt))));
        Assert.True(ShortcutPolicy.Evaluate(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Win)) is ShortcutPolicy.Verdict.Ok);
    }

    [Fact]
    public void ParsesShortcutsAsPeopleWriteThem()
    {
        Assert.Equal(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Win | KeyModifiers.Shift), KeyCombo.Parse("Win+Shift+Left"));
        Assert.Equal(new KeyCombo(KeyCode.R, KeyModifiers.Win | KeyModifiers.Control), KeyCombo.Parse("ctrl + win + r"));
        Assert.Equal(new KeyCombo(KeyCode.LeftBracket, KeyModifiers.Win), KeyCombo.Parse("Win+["));
        Assert.Equal(new KeyCombo(KeyCode.F1 + 4, KeyModifiers.Alt), KeyCombo.Parse("Alt+F5"));
        Assert.Equal(new KeyCombo(KeyCode.PageDown, KeyModifiers.Win), KeyCombo.Parse("Win+PgDn"));
        Assert.Equal(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Win | KeyModifiers.Control), KeyCombo.Parse("9:37"));
        Assert.Null(KeyCombo.Parse("Hyper+R"));
        Assert.Null(KeyCombo.Parse("Win+"));
        foreach (var combo in Keymap.Defaults.Values)
            Assert.Equal(combo, KeyCombo.Parse(combo.ToString())); // Display names read back
    }

    [Fact]
    public void ShortcutsFollowPhysicalKeyPositions()
    {
        // On a Spanish layout the key right of P is VK_OEM_1 ("`"); it still means Win+[.
        Assert.Equal(KeyCode.LeftBracket, KeyCode.ByPosition(0xBA, scanCode: 0x1A, extended: false));
        // AZERTY's A key sits where US has Q.
        Assert.Equal('Q', (char)KeyCode.ByPosition('A', scanCode: 0x10, extended: false));
        Assert.Equal('1', (char)KeyCode.ByPosition('1', scanCode: 0x02, extended: false));
        Assert.Equal(0x1Au, KeyCode.ScanCode(KeyCode.LeftBracket)); // Back from the key to where it is
        Assert.Null(KeyCode.ScanCode(KeyCode.LeftArrow));
        // Arrows are extended keys and keep their code.
        Assert.Equal(KeyCode.LeftArrow, KeyCode.ByPosition(KeyCode.LeftArrow, scanCode: 0x4B, extended: true));
    }

    [Fact]
    public void KnowsWhichDefaultsTakeOverWindowsFeatures()
    {
        Assert.Equal("Snap left or right", ShortcutPolicy.ReplacedWindowsFeature(Keymap.Defaults[GlideAction.FocusColumnLeft]));
        Assert.Equal("Task View", ShortcutPolicy.ReplacedWindowsFeature(Keymap.Defaults[GlideAction.ToggleOverview]));
        Assert.Equal("Switch virtual desktop", ShortcutPolicy.ReplacedWindowsFeature(Keymap.Defaults[GlideAction.FocusNextWorkspace]));
        Assert.Null(ShortcutPolicy.ReplacedWindowsFeature(Keymap.Defaults[GlideAction.ConsumeOrExpelLeft]));
        Assert.Null(ShortcutPolicy.ReplacedWindowsFeature(Keymap.Defaults[GlideAction.ToggleTiling]));
        Assert.Null(ShortcutPolicy.ReplacedWindowsFeature(new KeyCombo(KeyCode.LeftArrow, KeyModifiers.Alt)));
    }
}
