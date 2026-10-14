namespace Glide.Core;

public sealed record LayoutSettings
{
    public LayoutSettings(double gap = 12, double sliver = 4, IReadOnlyList<double>? widthPresets = null, int defaultWidthIndex = 1,
                          double tabBarHeight = 28, double tabBarSpacing = 4)
    {
        widthPresets ??= [1.0 / 3, 0.5, 2.0 / 3, 1];
        if (widthPresets.Count == 0) throw new ArgumentException("At least one width preset is required", nameof(widthPresets));
        Gap = gap;
        Sliver = sliver;
        WidthPresets = widthPresets;
        DefaultWidthIndex = Math.Clamp(defaultWidthIndex, 0, widthPresets.Count - 1);
        TabBarHeight = tabBarHeight;
        TabBarSpacing = tabBarSpacing;
    }

    /// <summary>Space between columns, between stacked windows, and around the viewport edge.</summary>
    public double Gap { get; init; }
    /// <summary>How much of an off-screen column stays visible at the screen edge.</summary>
    public double Sliver { get; init; }
    /// <summary>Fractions of the viewport a column can take, cycled in order.</summary>
    public IReadOnlyList<double> WidthPresets { get; init; }
    public int DefaultWidthIndex { get; init; }
    /// <summary>Height of a tabbed column's tab bar; the window sits <see cref="TabBarSpacing"/> below it.</summary>
    public double TabBarHeight { get; init; }
    public double TabBarSpacing { get; init; }

    public static LayoutSettings Standard { get; } = new();
}

/// <summary>Where a column sits relative to the viewport.</summary>
public enum Visibility
{
    Visible,
    /// <summary>Partly on screen at its real position.</summary>
    Partial,
    /// <summary>Entirely off screen: parked as a sliver at the left or right screen edge.</summary>
    ParkedLeft,
    ParkedRight,
    /// <summary>A tab that isn't showing. It waits in the bottom-right corner with a pixel or so showing.</summary>
    HiddenTab,
}

public readonly record struct WindowPlacement(WindowId Window, Rect Frame, Visibility Visibility);

/// <summary>The tab bar above a tabbed column that is at least partly on screen.</summary>
public sealed record TabBarPlacement(int Column, Rect Frame, IReadOnlyList<WindowId> Windows, int ActiveIndex)
{
    public WindowId ActiveWindow => Windows[ActiveIndex];

    public bool Equals(TabBarPlacement? other) =>
        other is not null && Column == other.Column && Frame == other.Frame && ActiveIndex == other.ActiveIndex && Windows.SequenceEqual(other.Windows);

    public override int GetHashCode() => HashCode.Combine(Column, Frame, ActiveIndex);
}

/// <summary>
/// A strip plus the geometry needed to lay it out: the viewport, the scroll position, and minimum
/// sizes learned from apps. Coordinates use a top-left origin, like Windows screen coordinates.
/// </summary>
public sealed class Workspace(Strip? strip, Rect viewport, LayoutSettings? settings = null)
{
    public Workspace(Rect viewport, LayoutSettings? settings = null) : this(null, viewport, settings) { }

    public Strip Strip { get; set; } = strip ?? new Strip();
    public Rect Viewport { get; set; } = viewport;
    public LayoutSettings Settings { get; set; } = settings ?? LayoutSettings.Standard;
    /// <summary>Strip coordinate shown at the left edge of the usable area.</summary>
    public double ScrollOffset { get; private set; }
    /// <summary>Widths that apps refused to shrink below, keyed by window.</summary>
    public Dictionary<WindowId, double> MinimumWidths { get; } = [];

    // Geometry

    public double UsableWidth => Math.Max(Viewport.Width - 2 * Settings.Gap, 0);

    /// <summary>
    /// Where hidden windows wait: hidden tabs, and every window of an inactive workspace. Leaves
    /// about a pixel showing, like the macOS app.
    /// </summary>
    public Point HiddenOrigin => new(Viewport.MaxX - 1, Viewport.MaxY - 1);

    public double Width(int columnIndex)
    {
        var column = Strip.Columns[columnIndex];
        var presets = Settings.WidthPresets;
        var fraction = column.IsFullWidth ? 1 : presets[Math.Clamp(column.WidthIndex, 0, presets.Count - 1)];
        var preset = column.IsFullWidth ? PresetWidth(fraction) : column.CustomWidth is { } custom ? Rounding.Round(custom) : PresetWidth(fraction);
        var minimum = column.Windows.Select(w => MinimumWidths.GetValueOrDefault(w)).DefaultIfEmpty(0).Max();
        return Math.Min(Math.Max(preset, minimum), UsableWidth);
    }

    /// <summary>Preset fractions are of the whole viewport, so two ½ columns exactly fill it, gaps included.</summary>
    public double PresetWidth(double fraction) => Rounding.Round((Viewport.Width - Settings.Gap) * fraction - Settings.Gap);

    /// <summary>The strip coordinate of each column's left edge.</summary>
    public IReadOnlyList<double> ColumnOffsets()
    {
        var offsets = new List<double>(Strip.Columns.Count);
        double x = 0;
        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            offsets.Add(x);
            x += Width(index) + Settings.Gap;
        }
        return offsets;
    }

    public double ContentWidth
    {
        get
        {
            if (Strip.IsEmpty) return 0;
            var last = Strip.Columns.Count - 1;
            return ColumnOffsets()[last] + Width(last);
        }
    }

    // Scrolling

    /// <summary>Scrolls as little as possible to bring the focused column fully on screen.</summary>
    public void EnsureFocusedVisible()
    {
        if (Strip.IsEmpty)
        {
            ScrollOffset = 0;
            return;
        }
        var index = Strip.FocusedColumnIndex;
        var left = ColumnOffsets()[index];
        var right = left + Width(index);
        if (left < ScrollOffset)
            ScrollOffset = left;
        else if (right > ScrollOffset + UsableWidth)
            ScrollOffset = right - UsableWidth;
    }

    public void CenterFocusedColumn()
    {
        if (Strip.IsEmpty) return;
        var index = Strip.FocusedColumnIndex;
        ScrollOffset = Rounding.Round(ColumnOffsets()[index] + Width(index) / 2 - UsableWidth / 2);
    }

    /// <summary>
    /// Removes empty space past either end of the strip, then keeps focus visible. Use after
    /// windows leave or the viewport changes.
    /// </summary>
    public void SettleScroll()
    {
        var maximum = Math.Max(ContentWidth - UsableWidth, 0);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, maximum);
        EnsureFocusedVisible();
    }

    // Gesture scrolling

    /// <summary>The scroll range that shows no empty space past the strip's ends.</summary>
    public (double Lower, double Upper) ScrollBounds => (0, Math.Max(ContentWidth - UsableWidth, 0));

    /// <summary>
    /// Moves the strip with the fingers. Past either end it follows at a third of the speed, up to
    /// a quarter of the screen, like a rubber band.
    /// </summary>
    public void Scroll(double delta)
    {
        var (lower, upper) = ScrollBounds;
        var limit = UsableWidth / 4;
        var outside = ScrollOffset < lower || ScrollOffset > upper || ScrollOffset + delta < lower || ScrollOffset + delta > upper;
        var next = ScrollOffset + (outside ? delta / 3 : delta);
        ScrollOffset = Math.Min(Math.Max(next, lower - limit), upper + limit);
    }

    /// <summary>
    /// The column-aligned scroll position nearest to <paramref name="offset"/>: some column's left
    /// edge at the left of the screen, or its right edge at the right, never past the strip's ends.
    /// </summary>
    public double SnappedOffset(double offset)
    {
        var (lower, upper) = ScrollBounds;
        var offsets = ColumnOffsets();
        var candidates = new List<double> { lower, upper };
        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            candidates.Add(offsets[index]);
            candidates.Add(offsets[index] + Width(index) - UsableWidth);
        }
        return candidates.Select(c => Math.Min(Math.Max(c, lower), upper)).MinBy(c => Math.Abs(c - offset));
    }

    /// <summary>
    /// Ends a swipe: snaps near where momentum would carry the strip, then keeps focus on a column
    /// that is fully on screen there (preferring the current one, else the one nearest the middle).
    /// Returns true if focus moved.
    /// </summary>
    public bool SettleSwipe(double projectedOffset)
    {
        if (Strip.IsEmpty)
        {
            ScrollOffset = 0;
            return false;
        }
        ScrollOffset = SnappedOffset(projectedOffset);
        var offsets = ColumnOffsets();
        bool FullyVisible(int index) =>
            offsets[index] >= ScrollOffset - 0.5 && offsets[index] + Width(index) <= ScrollOffset + UsableWidth + 0.5;

        if (FullyVisible(Strip.FocusedColumnIndex)) return false;
        var middle = ScrollOffset + UsableWidth / 2;
        var all = Enumerable.Range(0, Strip.Columns.Count).ToList();
        var candidates = all.Where(FullyVisible).ToList();
        var nearest = (candidates.Count == 0 ? all : candidates).MinBy(i => Math.Abs(offsets[i] + Width(i) / 2 - middle));
        Strip.Focus(Strip.Columns[nearest].FocusedWindow);
        EnsureFocusedVisible();
        return true;
    }

    // Actions

    /// <summary>Applies a layout action. Returns false if it changed nothing or is not a layout action.</summary>
    public bool Perform(GlideAction action)
    {
        bool changed;
        switch (action)
        {
            case GlideAction.FocusColumnLeft: changed = Strip.FocusColumn(HorizontalDirection.Left); break;
            case GlideAction.FocusColumnRight: changed = Strip.FocusColumn(HorizontalDirection.Right); break;
            case GlideAction.FocusFirstColumn: changed = Strip.FocusFirstColumn(); break;
            case GlideAction.FocusLastColumn: changed = Strip.FocusLastColumn(); break;
            case GlideAction.FocusWindowUp: changed = Strip.FocusWindow(VerticalDirection.Up); break;
            case GlideAction.FocusWindowDown: changed = Strip.FocusWindow(VerticalDirection.Down); break;
            case GlideAction.MoveColumnLeft: changed = Strip.MoveColumn(HorizontalDirection.Left); break;
            case GlideAction.MoveColumnRight: changed = Strip.MoveColumn(HorizontalDirection.Right); break;
            case GlideAction.MoveWindowUp: changed = Strip.MoveWindow(VerticalDirection.Up); break;
            case GlideAction.MoveWindowDown: changed = Strip.MoveWindow(VerticalDirection.Down); break;
            case GlideAction.ConsumeOrExpelLeft: changed = Strip.ConsumeOrExpel(HorizontalDirection.Left); break;
            case GlideAction.ConsumeOrExpelRight: changed = Strip.ConsumeOrExpel(HorizontalDirection.Right); break;
            case GlideAction.CycleColumnWidth: changed = CycleWidth(forward: true); break;
            case GlideAction.CycleColumnWidthBackward: changed = CycleWidth(forward: false); break;
            case GlideAction.ToggleFullWidth: changed = Strip.ToggleFullWidth(); break;
            case GlideAction.ToggleTabbed: changed = Strip.ToggleTabbed(); break;
            case GlideAction.CenterColumn:
                var before = ScrollOffset;
                CenterFocusedColumn();
                return ScrollOffset != before;
            default:
                // Workspace actions act on the whole WorkspaceSet; the rest belong to the app.
                return false;
        }
        if (changed)
        {
            if (action is GlideAction.ConsumeOrExpelLeft or GlideAction.ConsumeOrExpelRight)
                SettleScroll();
            else
                EnsureFocusedVisible();
        }
        return changed;
    }

    /// <summary>
    /// From a custom width, cycling moves to the next preset in that direction (wrapping);
    /// otherwise it steps through the presets.
    /// </summary>
    private bool CycleWidth(bool forward)
    {
        if (Strip.FocusedColumn is not { } column) return false;
        if (column.CustomWidth is not { } custom || column.IsFullWidth)
            return Strip.CycleWidth(Settings.WidthPresets.Count, forward);
        var widths = Settings.WidthPresets.Select(PresetWidth).ToList();
        var index = forward ? widths.FindIndex(w => w > custom + 1) : widths.FindLastIndex(w => w < custom - 1);
        if (index < 0) index = forward ? 0 : widths.Count - 1;
        return Strip.SetWidthIndex(index);
    }

    // Placement

    public IReadOnlyList<WindowPlacement> Placements()
    {
        var gap = Settings.Gap;
        var columnHeight = Math.Max(Viewport.Height - 2 * gap, 0);
        var result = new List<WindowPlacement>();

        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            var column = Strip.Columns[index];
            var (x, visibility) = Position(index);
            var width = Width(index);
            var top = Viewport.MinY + gap;

            if (column.IsTabbed)
            {
                var reserved = Settings.TabBarHeight + Settings.TabBarSpacing;
                var shown = new Rect(Rounding.Round(x), top + reserved, width, Math.Max(columnHeight - reserved, 0));
                for (var row = 0; row < column.Windows.Count; row++)
                {
                    var window = column.Windows[row];
                    result.Add(row == column.FocusedRow
                        ? new WindowPlacement(window, shown, visibility)
                        : new WindowPlacement(window, new Rect(HiddenOrigin, shown.Size), Visibility.HiddenTab));
                }
                continue;
            }

            var count = column.Windows.Count;
            var height = Math.Floor((columnHeight - gap * (count - 1)) / count);
            for (var row = 0; row < count; row++)
            {
                var frame = new Rect(Rounding.Round(x), top + row * (height + gap), width, height);
                result.Add(new WindowPlacement(column.Windows[row], frame, visibility));
            }
        }
        return result;
    }

    /// <summary>Tab bars for the tabbed columns that are at least partly on screen.</summary>
    public IReadOnlyList<TabBarPlacement> TabBars()
    {
        var result = new List<TabBarPlacement>();
        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            var column = Strip.Columns[index];
            var (x, visibility) = Position(index);
            if (!column.IsTabbed || visibility is not (Visibility.Visible or Visibility.Partial)) continue;
            var frame = new Rect(Rounding.Round(x), Viewport.MinY + Settings.Gap, Width(index), Settings.TabBarHeight);
            result.Add(new TabBarPlacement(index, frame, [.. column.Windows], column.FocusedRow));
        }
        return result;
    }

    /// <summary>A column's left edge on screen, and whether it is visible or parked at an edge.</summary>
    private (double X, Visibility Visibility) Position(int index)
    {
        var width = Width(index);
        var x = Viewport.MinX + Settings.Gap + ColumnOffsets()[index] - ScrollOffset;
        var overlap = Math.Min(x + width, Viewport.MaxX) - Math.Max(x, Viewport.MinX);
        if (x >= Viewport.MinX - 0.5 && x + width <= Viewport.MaxX + 0.5)
            return (x, Visibility.Visible);
        if (overlap >= Settings.Sliver)
            return (x, Visibility.Partial);
        if (x + width / 2 < Viewport.MidX)
            return (Viewport.MinX - width + Settings.Sliver, Visibility.ParkedLeft);
        return (Viewport.MaxX - Settings.Sliver, Visibility.ParkedRight);
    }
}
