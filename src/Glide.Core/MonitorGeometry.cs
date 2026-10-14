namespace Glide.Core;

/// <summary>Decisions about several monitors, from their screen rectangles alone.</summary>
public static class MonitorGeometry
{
    /// <summary>The monitor next to <paramref name="current"/> going left (-1) or right (+1), by horizontal position; null at the end.</summary>
    public static int? Neighbour(IReadOnlyList<Rect> monitors, int current, int direction)
    {
        var ordered = Enumerable.Range(0, monitors.Count).OrderBy(i => monitors[i].MidX).ThenBy(i => monitors[i].MidY).ToList();
        var index = ordered.IndexOf(current) + direction;
        return index >= 0 && index < ordered.Count ? ordered[index] : null;
    }

    /// <summary>Whether a window at <paramref name="frame"/> would show on a monitor other than <paramref name="own"/>.</summary>
    public static bool ShowsOnAnother(Rect frame, int own, IReadOnlyList<Rect> monitors) =>
        Enumerable.Range(0, monitors.Count).Any(i => i != own && monitors[i].Intersection(frame) is { Width: > 1, Height: > 1 });

    /// <summary>
    /// Where hidden windows wait: a pixel in from the bottom-right corner of the whole desktop,
    /// so at most a dot shows (usually under the taskbar).
    /// </summary>
    public static Point HiddenSpot(IReadOnlyList<Rect> monitors) => new(monitors.Max(m => m.MaxX) - 1, monitors.Max(m => m.MaxY) - 1);

    /// <summary>The column index a window dropped at screen x lands at: before the first column whose middle is right of it.</summary>
    public static int DropIndex(Workspace workspace, double x)
    {
        var offsets = workspace.ColumnOffsets();
        var stripX = x - workspace.Viewport.MinX - workspace.Settings.Gap + workspace.ScrollOffset;
        return Enumerable.Range(0, workspace.Strip.Columns.Count).Count(i => offsets[i] + workspace.Width(i) / 2 < stripX);
    }
}
