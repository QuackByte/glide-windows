using static Glide.Core.Strings;

namespace Glide.Core;

/// <summary>Which shortcut triggers which action: the defaults plus any user overrides.</summary>
public sealed class Keymap : IEquatable<Keymap>
{
    /// <summary>
    /// Win for focus, Win+Shift for "move", Win+Ctrl for column and workspace commands. These take
    /// over Windows' Snap, taskbar and Task View shortcuts; see the README.
    /// </summary>
    public static IReadOnlyDictionary<GlideAction, KeyCombo> Defaults { get; } = BuildDefaults();

    private static Dictionary<GlideAction, KeyCombo> BuildDefaults()
    {
        const KeyModifiers win = KeyModifiers.Win;
        const KeyModifiers move = KeyModifiers.Win | KeyModifiers.Shift;
        const KeyModifiers command = KeyModifiers.Win | KeyModifiers.Control;
        const KeyModifiers commandShift = KeyModifiers.Win | KeyModifiers.Control | KeyModifiers.Shift;
        var map = new Dictionary<GlideAction, KeyCombo>
        {
            [GlideAction.FocusColumnLeft] = new(KeyCode.LeftArrow, win),
            [GlideAction.FocusColumnRight] = new(KeyCode.RightArrow, win),
            [GlideAction.FocusWindowUp] = new(KeyCode.UpArrow, win),
            [GlideAction.FocusWindowDown] = new(KeyCode.DownArrow, win),
            [GlideAction.FocusFirstColumn] = new(KeyCode.Home, win),
            [GlideAction.FocusLastColumn] = new(KeyCode.End, win),
            [GlideAction.MoveColumnLeft] = new(KeyCode.LeftArrow, move),
            [GlideAction.MoveColumnRight] = new(KeyCode.RightArrow, move),
            [GlideAction.MoveWindowUp] = new(KeyCode.UpArrow, move),
            [GlideAction.MoveWindowDown] = new(KeyCode.DownArrow, move),
            [GlideAction.ConsumeOrExpelLeft] = new(KeyCode.LeftBracket, win),
            [GlideAction.ConsumeOrExpelRight] = new(KeyCode.RightBracket, win),
            [GlideAction.CycleColumnWidth] = new(KeyCode.R, command),
            [GlideAction.CycleColumnWidthBackward] = new(KeyCode.R, commandShift),
            [GlideAction.ToggleFullWidth] = new(KeyCode.F, command),
            [GlideAction.ToggleTabbed] = new(KeyCode.W, command),
            [GlideAction.CenterColumn] = new(KeyCode.C, command),
            [GlideAction.ToggleOverview] = new(KeyCode.Tab, win),
            [GlideAction.ToggleFloating] = new(KeyCode.V, command),
            [GlideAction.ToggleTiling] = new(KeyCode.T, commandShift),
            // The key with "?" on it, on US keyboards.
            [GlideAction.ShowShortcuts] = new(KeyCode.Slash, command),
            [GlideAction.FocusPreviousWorkspace] = new(KeyCode.LeftArrow, command),
            [GlideAction.FocusNextWorkspace] = new(KeyCode.RightArrow, command),
            [GlideAction.MoveColumnToPreviousWorkspace] = new(KeyCode.LeftArrow, commandShift),
            [GlideAction.MoveColumnToNextWorkspace] = new(KeyCode.RightArrow, commandShift),
            // Win+Alt for monitors: Windows uses Win+Shift+arrows for that, which Glide takes for moving columns.
            [GlideAction.FocusMonitorLeft] = new(KeyCode.LeftArrow, win | KeyModifiers.Alt),
            [GlideAction.FocusMonitorRight] = new(KeyCode.RightArrow, win | KeyModifiers.Alt),
            [GlideAction.MoveColumnToMonitorLeft] = new(KeyCode.LeftArrow, win | KeyModifiers.Alt | KeyModifiers.Shift),
            [GlideAction.MoveColumnToMonitorRight] = new(KeyCode.RightArrow, win | KeyModifiers.Alt | KeyModifiers.Shift),
        };
        // Win+1…9 switch workspaces; Win+Shift+1…9 send the focused column there.
        for (var index = 0; index < KeyCode.Digits.Count; index++)
        {
            map[GlideAction.FocusWorkspace1 + index] = new(KeyCode.Digits[index], win);
            map[GlideAction.MoveColumnToWorkspace1 + index] = new(KeyCode.Digits[index], move);
        }
        return map;
    }

    private readonly Dictionary<GlideAction, KeyCombo?> overrides;

    public Keymap(IReadOnlyDictionary<GlideAction, KeyCombo?>? overrides = null) =>
        this.overrides = overrides is null ? [] : new Dictionary<GlideAction, KeyCombo?>(overrides);

    /// <summary>Overrides of the defaults. A null value means the user removed the shortcut.</summary>
    public IReadOnlyDictionary<GlideAction, KeyCombo?> Overrides => overrides;

    public KeyCombo? Combo(GlideAction action) =>
        overrides.TryGetValue(action, out var combo) ? combo : Defaults.TryGetValue(action, out var standard) ? standard : null;

    public GlideAction? Action(KeyCombo combo)
    {
        foreach (var action in GlideActions.All)
            if (Combo(action) == combo) return action;
        return null;
    }

    /// <summary>Bound actions in declaration order.</summary>
    public IReadOnlyList<(GlideAction Action, KeyCombo Combo)> Bindings =>
        [.. GlideActions.All.Where(a => Combo(a) is not null).Select(a => (a, Combo(a)!.Value))];

    /// <summary>
    /// Binds a combo (or null to unbind). Any other action using the same combo is unbound so one
    /// key never triggers two actions; those actions are returned.
    /// </summary>
    public IReadOnlyList<GlideAction> Set(KeyCombo? combo, GlideAction action)
    {
        var displaced = new List<GlideAction>();
        if (combo is not null)
        {
            foreach (var other in GlideActions.All)
            {
                if (other == action || Combo(other) != combo) continue;
                Store(null, other);
                displaced.Add(other);
            }
        }
        Store(combo, action);
        return displaced;
    }

    public void Reset(GlideAction action)
    {
        var standard = Defaults.TryGetValue(action, out var value) ? value : (KeyCombo?)null;
        var displaced = GlideActions.All.Where(a => a != action && Combo(a) == standard).ToList();
        overrides.Remove(action);
        foreach (var other in displaced) Store(null, other);
    }

    public void ResetAll() => overrides.Clear();

    private void Store(KeyCombo? combo, GlideAction action)
    {
        if (combo == (Defaults.TryGetValue(action, out var standard) ? standard : null))
            overrides.Remove(action);
        else
            overrides[action] = combo;
    }

    // Persistence

    internal const string UnboundValue = "none";

    /// <summary>A JSON-friendly form for the settings file: only overrides are stored.</summary>
    public Dictionary<string, string> Storage =>
        overrides.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value?.StorageValue ?? UnboundValue);

    public static Keymap FromStorage(IReadOnlyDictionary<string, string> storage)
    {
        var overrides = new Dictionary<GlideAction, KeyCombo?>();
        foreach (var (key, value) in storage)
        {
            // Names only: Enum.TryParse would also take numbers, which shift as actions are added.
            if (int.TryParse(key, out _) || !Enum.TryParse<GlideAction>(key, out var action) || !Enum.IsDefined(action)) continue;
            if (value == UnboundValue)
                overrides[action] = null;
            else if (KeyCombo.FromStorage(value) is { } combo)
                overrides[action] = combo;
        }
        return new Keymap(overrides);
    }

    public bool Equals(Keymap? other) =>
        other is not null && overrides.Count == other.overrides.Count
        && overrides.All(pair => other.overrides.TryGetValue(pair.Key, out var combo) && combo == pair.Value);

    public override bool Equals(object? obj) => Equals(obj as Keymap);

    public override int GetHashCode() => overrides.Count;
}

/// <summary>Explains why a shortcut is a poor choice. Used to reject or warn while recording.</summary>
public static class ShortcutPolicy
{
    public abstract record Verdict
    {
        public sealed record Ok : Verdict;
        /// <summary>Allowed, but it shadows a Windows or text-editing shortcut.</summary>
        public sealed record Warning(string Message) : Verdict;
        /// <summary>Not usable as a global shortcut.</summary>
        public sealed record Rejected(string Message) : Verdict;
    }

    /// <summary>
    /// The Windows feature a combo takes over, if any: what Windows does with it when Glide
    /// doesn't swallow it. Glide can give these back (Settings › General).
    /// </summary>
    public static string? ReplacedWindowsFeature(KeyCombo combo)
    {
        const KeyModifiers win = KeyModifiers.Win, shift = KeyModifiers.Shift, ctrl = KeyModifiers.Control;
        var key = combo.KeyCode;
        return combo.Modifiers switch
        {
            win when key is KeyCode.LeftArrow or KeyCode.RightArrow => T("Snap left or right"),
            win when key == KeyCode.UpArrow => T("Maximize"),
            win when key == KeyCode.DownArrow => T("Restore or minimize"),
            win when key == KeyCode.Home => T("Minimize other windows"),
            win when key == KeyCode.Tab => T("Task View"),
            win when KeyCode.Digits.Contains(key) => T("Open the taskbar app in that position"),
            win | shift when key is KeyCode.LeftArrow or KeyCode.RightArrow => T("Move the window to another monitor"),
            win | shift when key is KeyCode.UpArrow or KeyCode.DownArrow => T("Stretch the window vertically"),
            win | shift when KeyCode.Digits.Contains(key) => T("Open another instance of the taskbar app"),
            win | ctrl when key is KeyCode.LeftArrow or KeyCode.RightArrow => T("Switch virtual desktop"),
            win | ctrl when key == KeyCode.D => T("New virtual desktop"),
            win | ctrl when key == KeyCode.F => T("Find computers"),
            win | ctrl when key == KeyCode.C => T("Color filters"),
            win | ctrl when key == KeyCode.V => T("Sound output"),
            _ => null,
        };
    }

    public static Verdict Evaluate(KeyCombo combo)
    {
        var modifiers = combo.Modifiers;
        var key = combo.KeyCode;
        var primary = modifiers & ~KeyModifiers.Shift;

        if (primary == KeyModifiers.None && !KeyCode.IsFunctionKey(key))
            return new Verdict.Rejected(T("Add Win, Ctrl or Alt so the shortcut doesn't block normal typing."));
        if (key == KeyCode.L && modifiers == KeyModifiers.Win)
            return new Verdict.Rejected(T("Windows locks the PC with Win+L before any app sees it."));
        if (key == KeyCode.Delete && modifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Alt))
            return new Verdict.Rejected(T("Ctrl+Alt+Del is reserved by Windows."));
        if (KeyCode.Arrows.Contains(key) || key == KeyCode.Home || key == KeyCode.End)
        {
            if (primary is KeyModifiers.Control or KeyModifiers.Alt or (KeyModifiers.Control | KeyModifiers.Alt))
                return new Verdict.Warning(T("{0}arrows move the cursor in text fields or go back in browsers.", modifiers.Prefix()));
        }
        if (modifiers == KeyModifiers.Alt && (key == KeyCode.Tab || key == KeyCode.Space || key == KeyCode.F1 + 3))
            return new Verdict.Warning(T("{0} is a standard Windows command.", combo));
        if (modifiers == KeyModifiers.Control && key is 'C' or 'V' or 'X' or 'Z' or 'S' or 'W' or (uint)KeyCode.Tab)
            return new Verdict.Warning(T("{0} is a standard Windows command.", combo));
        return new Verdict.Ok();
    }
}
