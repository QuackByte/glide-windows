using System.Text.Json;
using System.Text.Json.Serialization;

namespace Glide.Core;

/// <summary>
/// The parts of the layout worth keeping across a Glide quit or a tiling off/on cycle: each
/// workspace's column order, stacks, tabs and widths, and which windows the user floated or tiled
/// by hand.
/// </summary>
public sealed class StripSnapshot : IEquatable<StripSnapshot>
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public sealed record ColumnState(IReadOnlyList<WindowId> Windows, int FocusedRow, int WidthIndex, bool IsFullWidth,
                                     /* Optional so snapshots saved before tabs existed still load. */ bool? IsTabbed = null,
                                     double? CustomWidth = null)
    {
        public static ColumnState From(Column column) =>
            new([.. column.Windows], column.FocusedRow, column.WidthIndex, column.IsFullWidth, column.IsTabbed, column.CustomWidth);

        public bool Equals(ColumnState? other) =>
            other is not null && Windows.SequenceEqual(other.Windows) && FocusedRow == other.FocusedRow && WidthIndex == other.WidthIndex
            && IsFullWidth == other.IsFullWidth && IsTabbed == other.IsTabbed && CustomWidth == other.CustomWidth;

        public override int GetHashCode() => HashCode.Combine(Windows.Count, FocusedRow, WidthIndex);
    }

    public sealed record WorkspaceState(IReadOnlyList<ColumnState> Columns, WindowId? FocusedWindow,
                                        /* Optional so snapshots saved before the tiled layout existed still load; null follows the default. */ LayoutMode? Layout = null)
    {
        public static WorkspaceState From(Strip strip, LayoutMode? layout = null) =>
            new([.. strip.Columns.Select(ColumnState.From)], strip.FocusedWindow, layout);

        public bool Equals(WorkspaceState? other) =>
            other is not null && FocusedWindow == other.FocusedWindow && Layout == other.Layout && Columns.SequenceEqual(other.Columns);

        public override int GetHashCode() => HashCode.Combine(Columns.Count, FocusedWindow);
    }

    [JsonConstructor]
    public StripSnapshot(IReadOnlyList<ColumnState> columns, WindowId? focusedWindow, IReadOnlyList<WorkspaceState>? workspaces, int? activeWorkspace,
                         IReadOnlyList<WindowId> userFloating, IReadOnlyList<WindowId> userTiled, DateTimeOffset bootTime)
    {
        Columns = columns;
        FocusedWindow = focusedWindow;
        Workspaces = workspaces;
        ActiveWorkspace = activeWorkspace;
        UserFloating = userFloating;
        UserTiled = userTiled;
        BootTime = bootTime;
    }

    public StripSnapshot(Strip strip, IEnumerable<WindowId> userFloating, IEnumerable<WindowId> userTiled, DateTimeOffset bootTime)
    {
        var state = WorkspaceState.From(strip);
        Columns = state.Columns;
        FocusedWindow = state.FocusedWindow;
        UserFloating = [.. userFloating.Distinct().Order()];
        UserTiled = [.. userTiled.Distinct().Order()];
        BootTime = bootTime;
    }

    public StripSnapshot(WorkspaceSet set, IEnumerable<WindowId> userFloating, IEnumerable<WindowId> userTiled, DateTimeOffset bootTime)
        : this(set.Active.Strip, userFloating, userTiled, bootTime)
    {
        Workspaces = [.. set.Workspaces.Select(w => WorkspaceState.From(w.Strip, w.LayoutOverride))];
        ActiveWorkspace = set.ActiveIndex;
    }

    /// <summary>The active workspace. Also what snapshots from before workspaces hold.</summary>
    public IReadOnlyList<ColumnState> Columns { get; }
    public WindowId? FocusedWindow { get; }
    /// <summary>Every workspace in order; null in snapshots saved before workspaces existed.</summary>
    public IReadOnlyList<WorkspaceState>? Workspaces { get; }
    public int? ActiveWorkspace { get; }
    /// <summary>Windows the user floated, and floating-by-default windows the user tiled.</summary>
    public IReadOnlyList<WindowId> UserFloating { get; }
    public IReadOnlyList<WindowId> UserTiled { get; }
    /// <summary>Window handles are only unique until Windows restarts, so a snapshot from an earlier boot is ignored.</summary>
    public DateTimeOffset BootTime { get; }

    /// <summary>Each workspace's own layout, in order; null where it follows the default.</summary>
    [JsonIgnore]
    public IReadOnlyList<LayoutMode?> Layouts => [.. SavedWorkspaces.Select(w => w.Layout)];

    private IReadOnlyList<WorkspaceState> SavedWorkspaces => Workspaces ?? [new WorkspaceState(Columns, FocusedWindow)];

    /// <summary>Every window in the saved layout, workspace by workspace in strip order.</summary>
    [JsonIgnore]
    public IReadOnlyList<WindowId> AllWindows => [.. SavedWorkspaces.SelectMany(w => w.Columns.SelectMany(c => c.Windows))];

    /// <summary>
    /// Rebuilds the active workspace's strip from the windows that exist now. Saved columns come
    /// first, keeping their order, stacks and widths but dropping windows that have closed.
    /// Windows the snapshot doesn't know follow in the order given.
    /// </summary>
    public Strip Restore(IReadOnlyList<WindowId> windows, int widthIndex, int presetCount)
    {
        var (strips, active) = RestoreWorkspaces(windows, widthIndex, presetCount);
        return strips[active];
    }

    /// <summary>Rebuilds every workspace. Windows the snapshot doesn't know join the active workspace.</summary>
    public (IReadOnlyList<Strip> Strips, int Active) RestoreWorkspaces(IReadOnlyList<WindowId> windows, int widthIndex, int presetCount)
    {
        var states = SavedWorkspaces;
        var active = Math.Clamp(ActiveWorkspace ?? 0, 0, Math.Max(states.Count - 1, 0));
        var available = windows.ToHashSet();
        var placed = new HashSet<WindowId>();
        var strips = new List<Strip>();

        foreach (var state in states)
        {
            var restored = new List<Column>();
            foreach (var saved in state.Columns)
            {
                var kept = saved.Windows.Where(w => available.Contains(w) && !placed.Contains(w)).ToList();
                if (kept.Count == 0) continue;
                placed.UnionWith(kept);
                var previouslyFocused = saved.Windows[Math.Clamp(saved.FocusedRow, 0, saved.Windows.Count - 1)];
                var row = Math.Max(kept.IndexOf(previouslyFocused), 0);
                var width = saved.WidthIndex >= 0 && saved.WidthIndex < presetCount ? saved.WidthIndex : widthIndex;
                restored.Add(new Column(kept, row, width, saved.IsFullWidth, saved.IsTabbed ?? false, saved.CustomWidth));
            }
            var strip = new Strip(restored);
            if (state.FocusedWindow is { } focused) strip.Focus(focused);
            strips.Add(strip);
        }
        if (strips.Count == 0) strips.Add(new Strip());

        var columns = strips[active].Columns.ToList();
        foreach (var window in windows)
        {
            if (!placed.Add(window)) continue;
            columns.Add(new Column([window], widthIndex: widthIndex));
        }
        var activeFocus = strips[active].FocusedWindow;
        strips[active] = new Strip(columns);
        if (activeFocus is { } f) strips[active].Focus(f);
        return (strips, active);
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static StripSnapshot? FromJson(string json) => JsonSerializer.Deserialize<StripSnapshot>(json, JsonOptions);

    public bool Equals(StripSnapshot? other) =>
        other is not null && Columns.SequenceEqual(other.Columns) && FocusedWindow == other.FocusedWindow
        && (Workspaces is null ? other.Workspaces is null : other.Workspaces is not null && Workspaces.SequenceEqual(other.Workspaces))
        && ActiveWorkspace == other.ActiveWorkspace && UserFloating.SequenceEqual(other.UserFloating)
        && UserTiled.SequenceEqual(other.UserTiled) && BootTime == other.BootTime;

    public override bool Equals(object? obj) => Equals(obj as StripSnapshot);

    public override int GetHashCode() => HashCode.Combine(Columns.Count, FocusedWindow, BootTime);
}
