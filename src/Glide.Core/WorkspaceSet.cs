namespace Glide.Core;

/// <summary>
/// Numbered workspaces, each its own strip with its own scroll position. One is active; the
/// windows of the others are hidden. Windows join the active workspace.
/// </summary>
public sealed class WorkspaceSet
{
    public const int Count = 9;

    private readonly Workspace[] workspaces;

    public WorkspaceSet(Rect viewport, LayoutSettings? settings = null) =>
        workspaces = [.. Enumerable.Range(0, Count).Select(_ => new Workspace(viewport, settings))];

    public IReadOnlyList<Workspace> Workspaces => workspaces;
    public int ActiveIndex { get; private set; }

    public Workspace Active => workspaces[ActiveIndex];

    public Rect Viewport
    {
        get => Active.Viewport;
        set { foreach (var workspace in workspaces) workspace.Viewport = value; }
    }

    public LayoutSettings Settings
    {
        get => Active.Settings;
        set { foreach (var workspace in workspaces) workspace.Settings = value; }
    }

    /// <summary>Tiled windows on every workspace, workspace by workspace.</summary>
    public IReadOnlyList<WindowId> AllWindows => [.. workspaces.SelectMany(w => w.Strip.Windows)];

    public int? IndexOf(WindowId window)
    {
        var index = Array.FindIndex(workspaces, w => w.Strip.Contains(window));
        return index < 0 ? null : index;
    }

    /// <summary>Indices of workspaces that hold windows.</summary>
    public IReadOnlyList<int> Occupied => [.. Enumerable.Range(0, Count).Where(i => !workspaces[i].Strip.IsEmpty)];

    private static bool IsValid(int index) => index is >= 0 and < Count;

    /// <summary>Removes a window from whichever workspace has it, closing up the gap it leaves.</summary>
    public int? Remove(WindowId window)
    {
        if (IndexOf(window) is not int index) return null;
        workspaces[index].Strip.Remove(window);
        workspaces[index].MinimumWidths.Remove(window);
        workspaces[index].SettleScroll();
        return index;
    }

    public int? Resolve(WorkspaceTarget target)
    {
        var index = target switch
        {
            WorkspaceTarget.Index(var value) => value,
            WorkspaceTarget.Previous => ActiveIndex - 1,
            WorkspaceTarget.Next => ActiveIndex + 1,
            _ => -1,
        };
        return IsValid(index) ? index : null;
    }

    /// <summary>Makes another workspace active. Returns false if it already is, or doesn't exist.</summary>
    public bool Activate(int index)
    {
        if (!IsValid(index) || index == ActiveIndex) return false;
        ActiveIndex = index;
        workspaces[index].SettleScroll();
        return true;
    }

    /// <summary>
    /// Sends the active workspace's focused column to another workspace, where it lands right of
    /// that workspace's focused column and takes its focus. The active workspace stays.
    /// </summary>
    public bool MoveFocusedColumn(int index)
    {
        if (!IsValid(index) || index == ActiveIndex || workspaces[ActiveIndex].Strip.RemoveFocusedColumn() is not { } column) return false;
        workspaces[ActiveIndex].SettleScroll();
        workspaces[index].Strip.Insert(column);
        workspaces[index].EnsureFocusedVisible();
        return true;
    }

    /// <summary>
    /// Adds a new window as its own column on a workspace (the active one by default), right of
    /// that workspace's focused column. Returns the workspace it joined.
    /// </summary>
    public int Insert(WindowId window, int widthIndex, int? workspace = null, bool focus = true)
    {
        var target = workspace is int requested && IsValid(requested) ? requested : ActiveIndex;
        workspaces[target].Strip.Insert(window, widthIndex, focus: focus);
        if (target == ActiveIndex || focus) workspaces[target].EnsureFocusedVisible();
        return target;
    }

    // Windows that leave and come back

    /// <summary>Where a window was, so it can return there after it is hidden or minimized.</summary>
    /// <param name="Sibling">Another window in the same column, to rejoin.</param>
    /// <param name="LeftNeighbour">The first window of the column to the left; null if it was the first column.</param>
    public sealed record Departure(int Workspace, WindowId? Sibling, WindowId? LeftNeighbour, int WidthIndex, bool IsFullWidth, double? CustomWidth);

    public Departure? DepartureOf(WindowId window)
    {
        if (IndexOf(window) is not int workspace || workspaces[workspace].Strip.Location(window) is not (int columnIndex, _)) return null;
        var strip = workspaces[workspace].Strip;
        var column = strip.Columns[columnIndex];
        return new Departure(
            workspace,
            column.Windows.Where(w => w != window).Select(w => (WindowId?)w).FirstOrDefault(),
            columnIndex > 0 ? strip.Columns[columnIndex - 1].Windows[0] : null,
            column.WidthIndex,
            column.IsFullWidth,
            column.CustomWidth);
    }

    /// <summary>
    /// Puts a returning window back where it left: into its old column if a sibling is still
    /// there, else as its own column right of its old left neighbour. Returns the workspace.
    /// </summary>
    public int Rejoin(WindowId window, Departure departure, bool focus)
    {
        var target = IsValid(departure.Workspace) ? departure.Workspace : ActiveIndex;
        var strip = workspaces[target].Strip;
        if (departure.Sibling is not { } sibling || !strip.Join(window, sibling, focus))
        {
            int? position = departure.LeftNeighbour is { } neighbour
                ? strip.Location(neighbour) is (int column, _) ? column + 1 : null
                : 0;
            strip.Insert(new Column([window], widthIndex: departure.WidthIndex, isFullWidth: departure.IsFullWidth, customWidth: departure.CustomWidth),
                         position, focus);
        }
        workspaces[target].EnsureFocusedVisible();
        return target;
    }

    /// <summary>Rebuilds the set from saved strips, e.g. after a relaunch.</summary>
    public void Restore(IReadOnlyList<Strip> strips, int activeIndex)
    {
        for (var index = 0; index < Count; index++)
        {
            workspaces[index].Strip = index < strips.Count ? strips[index] : new Strip();
            workspaces[index].SettleScroll();
        }
        ActiveIndex = IsValid(activeIndex) ? activeIndex : 0;
    }
}
