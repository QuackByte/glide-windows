using System.Text.Json.Serialization;

namespace Glide.Core;

/// <summary>How a workspace arranges its columns. Names are persisted; do not rename them.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LayoutMode>))]
public enum LayoutMode
{
    /// <summary>Columns keep their preset widths on a strip that scrolls.</summary>
    Scrolling,
    /// <summary>Every window fits on screen, tiled in a spiral: columns first, then the last column splits again and again.</summary>
    Tiled,
}

public sealed record LayoutSettings
{
    public LayoutSettings(double gap = 12, double sliver = 4, IReadOnlyList<double>? widthPresets = null, int defaultWidthIndex = 1,
                          double tabBarHeight = 28, double tabBarSpacing = 4, LayoutMode defaultLayout = LayoutMode.Scrolling,
                          int? defaultTiledColumns = null)
    {
        widthPresets ??= [1.0 / 3, 0.5, 2.0 / 3, 1];
        if (widthPresets.Count == 0) throw new ArgumentException("At least one width preset is required", nameof(widthPresets));
        Gap = gap;
        Sliver = sliver;
        WidthPresets = widthPresets;
        DefaultWidthIndex = Math.Clamp(defaultWidthIndex, 0, widthPresets.Count - 1);
        TabBarHeight = tabBarHeight;
        TabBarSpacing = tabBarSpacing;
        DefaultLayout = defaultLayout;
        DefaultTiledColumns = defaultTiledColumns is { } columns ? TiledLayout.ClampColumns(columns) : null;
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
    /// <summary>The layout of every workspace the user hasn't switched by hand.</summary>
    public LayoutMode DefaultLayout { get; init; }
    /// <summary>How many columns tiled workspaces without their own count use, 2 or 3; null picks by the screen's shape.</summary>
    public int? DefaultTiledColumns { get; init; }

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

    /// <summary>This workspace's own layout, once the user has switched it; null follows <see cref="LayoutSettings.DefaultLayout"/>.</summary>
    public LayoutMode? LayoutOverride { get; set; }
    public LayoutMode Layout => LayoutOverride ?? Settings.DefaultLayout;

    /// <summary>This workspace's own tiled column count (2 or 3), once the user has switched it; null follows the default.</summary>
    public int? TiledColumnsOverride { get; set; }
    /// <summary>How many columns this workspace tiles into: its own count, else the default, else 3 on an ultrawide screen and 2 elsewhere.</summary>
    public int TiledColumns => TiledLayout.ClampColumns(TiledColumnsOverride ?? Settings.DefaultTiledColumns ?? TiledLayout.AutoColumns(Viewport));

    /// <summary>
    /// What the first part of each split gets, by split (see <see cref="TiledLayout"/>); 0 keeps a split's default.
    /// Only used while it has one entry per split, so it starts over when windows come or go.
    /// </summary>
    public IReadOnlyList<double>? TiledRatios { get; set; }

    /// <summary>The window whose tile fills the screen (tiled "full width"); only counts while that tile has focus.</summary>
    public WindowId? ZoomedWindow { get; private set; }

    /// <summary>
    /// Switches between scrolling and tiled, for this workspace only. Going tiled gives every
    /// window of a stack its own column; going back to scrolling gives every column the default width.
    /// </summary>
    public void ToggleLayout()
    {
        LayoutOverride = Layout == LayoutMode.Scrolling ? LayoutMode.Tiled : LayoutMode.Scrolling;
        if (Layout == LayoutMode.Scrolling)
        {
            Strip.ResetWidths(Settings.DefaultWidthIndex);
            ZoomedWindow = null;
        }
        SettleScroll();
    }

    /// <summary>Switches this workspace between 2 and 3 tiled columns, starting its splits over.</summary>
    public void CycleTiledColumns()
    {
        TiledColumnsOverride = TiledColumns == TiledLayout.MinColumns ? TiledLayout.MaxColumns : TiledLayout.MinColumns;
        TiledRatios = null;
    }

    // Geometry

    public double UsableWidth => Math.Max(Viewport.Width - 2 * Settings.Gap, 0);

    /// <summary>
    /// Where hidden windows wait: hidden tabs, and every window of an inactive workspace. Leaves
    /// about a pixel showing, like the macOS app.
    /// </summary>
    public Point HiddenOrigin => new(Viewport.MaxX - 1, Viewport.MaxY - 1);

    public double Width(int columnIndex) => ScrollingWidth(columnIndex);

    /// <summary>A column's width on the strip: its preset or custom width, at least as wide as its windows allow.</summary>
    private double ScrollingWidth(int columnIndex)
    {
        var column = Strip.Columns[columnIndex];
        var presets = Settings.WidthPresets;
        var preset = column.IsFullWidth ? PresetWidth(1)
            : column.CustomWidth is { } custom ? Rounding.Round(custom)
            : PresetWidth(presets[Math.Clamp(column.WidthIndex, 0, presets.Count - 1)]);
        return Math.Min(Math.Max(preset, MinimumWidth(column)), UsableWidth);
    }

    private double MinimumWidth(Column column) => column.Windows.Select(w => MinimumWidths.GetValueOrDefault(w)).DefaultIfEmpty(0).Max();

    /// <summary>Every column's width on the strip, worked out once.</summary>
    private List<double> Widths() => [.. Enumerable.Range(0, Strip.Columns.Count).Select(ScrollingWidth)];

    /// <summary>Preset fractions are of the whole viewport, so two ½ columns exactly fill it, gaps included.</summary>
    public double PresetWidth(double fraction) => Rounding.Round((Viewport.Width - Settings.Gap) * fraction - Settings.Gap);

    /// <summary>The strip coordinate of each column's left edge.</summary>
    public IReadOnlyList<double> ColumnOffsets() => Offsets(Widths());

    private List<double> Offsets(IReadOnlyList<double> widths)
    {
        var offsets = new List<double>(widths.Count);
        double x = 0;
        foreach (var width in widths)
        {
            offsets.Add(x);
            x += width + Settings.Gap;
        }
        return offsets;
    }

    public double ContentWidth
    {
        get
        {
            if (Strip.IsEmpty) return 0;
            var widths = Widths();
            return Offsets(widths)[^1] + widths[^1];
        }
    }

    // Scrolling

    /// <summary>
    /// Tiled workspaces don't scroll: everything is on screen. They keep one tile per column
    /// instead (a window dropped onto another's column becomes the next tile) and forget a zoom
    /// whose window has gone.
    /// </summary>
    private void KeepTiledShape()
    {
        Strip.ExplodeStacks();
        ScrollOffset = 0;
        if (ZoomedWindow is { } zoomed && !Strip.Contains(zoomed)) ZoomedWindow = null;
    }

    /// <summary>Scrolls as little as possible to bring the focused column fully on screen.</summary>
    public void EnsureFocusedVisible()
    {
        if (Layout == LayoutMode.Tiled)
        {
            KeepTiledShape();
            return;
        }
        if (Strip.IsEmpty)
        {
            ScrollOffset = 0;
            return;
        }
        var index = Strip.FocusedColumnIndex;
        var widths = Widths();
        var left = Offsets(widths)[index];
        var right = left + widths[index];
        if (left < ScrollOffset)
            ScrollOffset = left;
        else if (right > ScrollOffset + UsableWidth)
            ScrollOffset = right - UsableWidth;
    }

    public void CenterFocusedColumn()
    {
        if (Strip.IsEmpty || Layout == LayoutMode.Tiled) return;
        var index = Strip.FocusedColumnIndex;
        var widths = Widths();
        ScrollOffset = Rounding.Round(Offsets(widths)[index] + widths[index] / 2 - UsableWidth / 2);
    }

    /// <summary>
    /// Removes empty space past either end of the strip, then keeps focus visible. Use after
    /// windows leave or the viewport changes.
    /// </summary>
    public void SettleScroll()
    {
        if (Layout == LayoutMode.Tiled)
        {
            KeepTiledShape();
            return;
        }
        var maximum = Math.Max(ContentWidth - UsableWidth, 0);
        ScrollOffset = Math.Clamp(ScrollOffset, 0, maximum);
        EnsureFocusedVisible();
    }

    // Gesture scrolling

    /// <summary>The scroll range that shows no empty space past the strip's ends.</summary>
    public (double Lower, double Upper) ScrollBounds => (0, Layout == LayoutMode.Tiled ? 0 : Math.Max(ContentWidth - UsableWidth, 0));

    /// <summary>
    /// Moves the strip with the fingers. Past either end it follows at a third of the speed, up to
    /// a quarter of the screen, like a rubber band.
    /// </summary>
    public void Scroll(double delta)
    {
        if (Layout == LayoutMode.Tiled) return;
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
        var widths = Widths();
        var offsets = Offsets(widths);
        var candidates = new List<double> { lower, upper };
        for (var index = 0; index < widths.Count; index++)
        {
            candidates.Add(offsets[index]);
            candidates.Add(offsets[index] + widths[index] - UsableWidth);
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
        if (Strip.IsEmpty || Layout == LayoutMode.Tiled)
        {
            ScrollOffset = 0;
            return false;
        }
        ScrollOffset = SnappedOffset(projectedOffset);
        var widths = Widths();
        var offsets = Offsets(widths);
        bool FullyVisible(int index) =>
            offsets[index] >= ScrollOffset - 0.5 && offsets[index] + widths[index] <= ScrollOffset + UsableWidth + 0.5;

        if (FullyVisible(Strip.FocusedColumnIndex)) return false;
        var middle = ScrollOffset + UsableWidth / 2;
        var all = Enumerable.Range(0, Strip.Columns.Count).ToList();
        var candidates = all.Where(FullyVisible).ToList();
        var nearest = (candidates.Count == 0 ? all : candidates).MinBy(i => Math.Abs(offsets[i] + widths[i] / 2 - middle));
        Strip.Focus(Strip.Columns[nearest].FocusedWindow);
        EnsureFocusedVisible();
        return true;
    }

    // Actions

    /// <summary>Applies a layout action. Returns false if it changed nothing or is not a layout action.</summary>
    public bool Perform(GlideAction action)
    {
        if (action == GlideAction.CycleTiledColumns)
        {
            CycleTiledColumns();
            return true;
        }
        if (Layout == LayoutMode.Tiled && PerformTiled(action) is { } tiledChanged) return tiledChanged;
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
            case GlideAction.ToggleLayout:
                ToggleLayout();
                return true;
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
            // Tiled keeps one tile per column, so a tab toggle can add or remove tiles; a stack's expelled window may close a gap.
            if (action is GlideAction.ConsumeOrExpelLeft or GlideAction.ConsumeOrExpelRight || Layout == LayoutMode.Tiled)
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

    /// <summary>
    /// Adopts the width the user just dragged <paramref name="window"/> to. On the strip that is its
    /// column's width. Tiled, it moves the nearest column split that decides the tile's width, so
    /// the layout stays a spiral; a tile whose width no split decides (it is alone) snaps back.
    /// Returns false if nothing changed.
    /// </summary>
    public bool SetColumnWidthFromResize(WindowId window, double width)
    {
        if (Layout == LayoutMode.Scrolling) return Strip.SetCustomWidth(width, window);
        var tiles = Tiles();
        var index = tiles.FindIndex(t => TileWindows(t).Contains(window));
        if (index < 0 || ZoomedWindow is not null) return false;
        var count = tiles.Count;
        var splits = TiledLayout.Splits(Viewport, count, TiledColumns, Settings.Gap, EffectiveRatios(count));
        // The split that sizes the tile across: its own if that one is side by side (the tile is its first part), else the nearest earlier one (it is in the second part).
        int split;
        bool first;
        if (index < count - 1 && splits[index].SideBySide)
            (split, first) = (index, true);
        else if (Enumerable.Range(0, Math.Min(index, count - 1)).Reverse().Where(j => splits[j].SideBySide).Cast<int?>().FirstOrDefault() is int earlier)
            (split, first) = (earlier, false);
        else
            return false;
        var available = splits[split].Room.Width - Settings.Gap;
        if (available <= 0) return false;
        var share = Math.Clamp(width / available, 0.1, 0.9);
        return SetTiledRatio(split, count, first ? share : 1 - share);
    }

    // Tiled

    /// <summary>A tile: a column's one window, or a whole tabbed column (<see cref="Row"/> is then null).</summary>
    private readonly record struct Tile(int Column, int? Row);

    /// <summary>The tiles in order: columns left to right, a stack's windows top to bottom.</summary>
    private List<Tile> Tiles()
    {
        var tiles = new List<Tile>();
        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            var column = Strip.Columns[index];
            if (column.IsTabbed)
                tiles.Add(new Tile(index, null));
            else
                tiles.AddRange(Enumerable.Range(0, column.Windows.Count).Select(row => new Tile(index, row)));
        }
        return tiles;
    }

    private IReadOnlyList<WindowId> TileWindows(Tile tile) =>
        tile.Row is int row ? [Strip.Columns[tile.Column].Windows[row]] : Strip.Columns[tile.Column].Windows;

    /// <summary>The splits' ratios, if they were set for this many tiles.</summary>
    private IReadOnlyList<double>? EffectiveRatios(int count) => TiledRatios is { } ratios && ratios.Count == count - 1 ? ratios : null;

    /// <summary>The tile holding the zoomed window, while it has focus.</summary>
    private int? ZoomedTile(IReadOnlyList<Tile> tiles)
    {
        if (ZoomedWindow is not { } zoomed || Strip.FocusedColumn is not { } focused || !focused.Windows.Contains(zoomed)) return null;
        var index = tiles.ToList().FindIndex(t => t.Column == Strip.FocusedColumnIndex && (t.Row is null || focused.Windows[t.Row.Value] == zoomed));
        return index < 0 ? null : index;
    }

    /// <summary>
    /// Every tile's frame as the spiral lays it out, before minimum widths and zoom: what focus
    /// and move go by, and what the ratios shape.
    /// </summary>
    private List<Rect> TileFrames(IReadOnlyList<Tile> tiles) =>
        [.. TiledLayout.Frames(Viewport, tiles.Count, TiledColumns, Settings.Gap, EffectiveRatios(tiles.Count))];

    /// <summary>
    /// The frames tiles are sent to: a tile narrower than a window in it allows gets that window's
    /// minimum width within its own x, overlapping its neighbour if it must; a zoomed tile fills the screen.
    /// </summary>
    private List<Rect> PlacedFrames(IReadOnlyList<Tile> tiles)
    {
        var frames = TileFrames(tiles);
        var right = Viewport.MaxX - Settings.Gap;
        for (var index = 0; index < frames.Count; index++)
        {
            var minimum = TileWindows(tiles[index]).Select(w => MinimumWidths.GetValueOrDefault(w)).DefaultIfEmpty(0).Max();
            if (frames[index].Width >= minimum) continue;
            var width = Math.Min(minimum, UsableWidth);
            frames[index] = frames[index] with { X = Math.Min(frames[index].X, Math.Max(right - width, Viewport.MinX + Settings.Gap)), Width = width };
        }
        if (ZoomedTile(tiles) is int zoomed)
            frames[zoomed] = new Rect(Viewport.MinX + Settings.Gap, Viewport.MinY + Settings.Gap, UsableWidth, Math.Max(Viewport.Height - 2 * Settings.Gap, 0));
        return frames;
    }

    /// <summary>Focus, move, width and zoom in a tiled workspace. Null for actions that work as they do on the strip.</summary>
    private bool? PerformTiled(GlideAction action)
    {
        KeepTiledShape();
        if (Strip.IsEmpty) return action is GlideAction.ToggleLayout or GlideAction.ToggleTabbed or GlideAction.CenterColumn ? null : false;
        var changed = PerformTiledAction(action);
        // A zoom ends when focus leaves its tile.
        if (ZoomedWindow is { } zoomed && Strip.FocusedColumn?.Windows.Contains(zoomed) != true) ZoomedWindow = null;
        return changed;
    }

    private bool? PerformTiledAction(GlideAction action)
    {
        switch (action)
        {
            case GlideAction.FocusColumnLeft: return FocusTile(TileSide.Left);
            case GlideAction.FocusColumnRight: return FocusTile(TileSide.Right);
            case GlideAction.FocusWindowUp: return FocusTile(TileSide.Up);
            case GlideAction.FocusWindowDown: return FocusTile(TileSide.Down);
            case GlideAction.MoveColumnLeft: return MoveTile(TileSide.Left);
            case GlideAction.MoveColumnRight: return MoveTile(TileSide.Right);
            case GlideAction.MoveWindowUp: return MoveTile(TileSide.Up);
            case GlideAction.MoveWindowDown: return MoveTile(TileSide.Down);
            case GlideAction.FocusFirstColumn: return Strip.FocusFirstColumn();
            case GlideAction.FocusLastColumn: return Strip.FocusLastColumn();
            case GlideAction.ConsumeOrExpelLeft: return ConsumeOrExpelTile(HorizontalDirection.Left);
            case GlideAction.ConsumeOrExpelRight: return ConsumeOrExpelTile(HorizontalDirection.Right);
            case GlideAction.CycleColumnWidth: return CycleTiledRatio(forward: true);
            case GlideAction.CycleColumnWidthBackward: return CycleTiledRatio(forward: false);
            case GlideAction.ToggleFullWidth:
                ZoomedWindow = ZoomedTile(Tiles()) is null ? Strip.FocusedWindow : null;
                return true;
            default: return null;
        }
    }

    /// <summary>
    /// A window in a tab group with others is expelled into a new tile on that side. A window alone
    /// in its tile joins the tile next to it on that side as a tab, which makes that tile tabbed so
    /// it stays one tile. Returns false if there is no tile on that side.
    /// </summary>
    private bool ConsumeOrExpelTile(HorizontalDirection direction)
    {
        if (Strip.FocusedColumn is not { } column) return false;
        if (column.Windows.Count > 1) return Strip.ConsumeOrExpel(direction);
        var side = direction == HorizontalDirection.Left ? TileSide.Left : TileSide.Right;
        if (TiledLayout.Neighbour(TileFrames(Tiles()), Strip.FocusedColumnIndex, side) is not int target) return false;
        var window = column.FocusedWindow;
        var sibling = Strip.Columns[target].FocusedWindow;
        Strip.Remove(window);
        Strip.Join(window, sibling);
        Strip.Columns[Strip.FocusedColumnIndex].IsTabbed = true;
        return true;
    }

    /// <summary>Moves focus to the tile next to the focused one on a side.</summary>
    private bool FocusTile(TileSide side) =>
        TiledLayout.Neighbour(TileFrames(Tiles()), Strip.FocusedColumnIndex, side) is int target && Strip.FocusColumnAt(target);

    /// <summary>Swaps the focused tile with the one next to it on a side; focus goes with the window.</summary>
    private bool MoveTile(TileSide side) =>
        TiledLayout.Neighbour(TileFrames(Tiles()), Strip.FocusedColumnIndex, side) is int target && Strip.SwapColumns(Strip.FocusedColumnIndex, target);

    /// <summary>
    /// Steps the split that cuts the focused tile off from the tiles after it through 1/3, 1/2 and
    /// 2/3 of its room, for the tile's share of it. The last tile has no split of its own and
    /// changes the one before it.
    /// </summary>
    private bool CycleTiledRatio(bool forward)
    {
        var count = Strip.Columns.Count;
        if (count < 2) return false;
        var index = Strip.FocusedColumnIndex;
        var split = Math.Min(index, count - 2);
        var ratios = EffectiveRatios(count);
        var current = TiledLayout.Ratio(split, count, TiledColumns, ratios);
        var share = index == count - 1 ? 1 - current : current;
        var presets = TiledLayout.RatioPresets;
        const double tolerance = 0.01;
        var next = forward ? presets.Where(p => p > share + tolerance).DefaultIfEmpty(presets[0]).First()
                           : presets.Where(p => p < share - tolerance).DefaultIfEmpty(presets[^1]).Last();
        return SetTiledRatio(split, count, index == count - 1 ? 1 - next : next);
    }

    /// <summary>Sets one split's ratio, keeping the others as they are. Returns false if it already was that.</summary>
    private bool SetTiledRatio(int split, int count, double ratio)
    {
        var current = EffectiveRatios(count);
        var updated = Enumerable.Range(0, count - 1).Select(k => TiledLayout.IsSet(k, current) ? current![k] : 0).ToList();
        if (Math.Abs(updated[split] - ratio) < 0.001) return false;
        updated[split] = ratio;
        TiledRatios = updated;
        return true;
    }

    /// <summary>Where a window dropped at screen x lands among the tiles: after those whose middle is left of it.</summary>
    public int TileDropIndex(double x) => TileFrames(Tiles()).Count(f => f.MidX < x);

    // Placement

    public IReadOnlyList<WindowPlacement> Placements() => Layout == LayoutMode.Tiled ? TiledPlacements() : ScrollingPlacements();

    private List<WindowPlacement> TiledPlacements()
    {
        var tiles = Tiles();
        var frames = PlacedFrames(tiles);
        var result = new List<WindowPlacement>();
        for (var index = 0; index < tiles.Count; index++)
        {
            var column = Strip.Columns[tiles[index].Column];
            if (!column.IsTabbed)
            {
                result.Add(new WindowPlacement(column.Windows[tiles[index].Row!.Value], frames[index], Visibility.Visible));
                continue;
            }
            var reserved = Settings.TabBarHeight + Settings.TabBarSpacing;
            var shown = new Rect(frames[index].X, frames[index].Y + reserved, frames[index].Width, Math.Max(frames[index].Height - reserved, 0));
            for (var row = 0; row < column.Windows.Count; row++)
            {
                result.Add(row == column.FocusedRow
                    ? new WindowPlacement(column.Windows[row], shown, Visibility.Visible)
                    : new WindowPlacement(column.Windows[row], new Rect(HiddenOrigin, shown.Size), Visibility.HiddenTab));
            }
        }
        return result;
    }

    private List<WindowPlacement> ScrollingPlacements()
    {
        var gap = Settings.Gap;
        var columnHeight = Math.Max(Viewport.Height - 2 * gap, 0);
        var result = new List<WindowPlacement>();
        var widths = Widths();
        var offsets = Offsets(widths);

        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            var column = Strip.Columns[index];
            var width = widths[index];
            var (x, visibility) = Position(width, offsets[index]);
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
        if (Layout == LayoutMode.Tiled)
        {
            var tiles = Tiles();
            var frames = PlacedFrames(tiles);
            for (var index = 0; index < tiles.Count; index++)
            {
                var column = Strip.Columns[tiles[index].Column];
                if (!column.IsTabbed) continue;
                var frame = new Rect(frames[index].X, frames[index].Y, frames[index].Width, Settings.TabBarHeight);
                result.Add(new TabBarPlacement(tiles[index].Column, frame, [.. column.Windows], column.FocusedRow));
            }
            return result;
        }
        var widths = Widths();
        var offsets = Offsets(widths);
        for (var index = 0; index < Strip.Columns.Count; index++)
        {
            var column = Strip.Columns[index];
            var (x, visibility) = Position(widths[index], offsets[index]);
            if (!column.IsTabbed || visibility is not (Visibility.Visible or Visibility.Partial)) continue;
            var frame = new Rect(Rounding.Round(x), Viewport.MinY + Settings.Gap, widths[index], Settings.TabBarHeight);
            result.Add(new TabBarPlacement(index, frame, [.. column.Windows], column.FocusedRow));
        }
        return result;
    }

    /// <summary>
    /// Where a window dropped on <paramref name="window"/> would go, to highlight while dragging: the
    /// part of its column on screen, tab bar included. Null when the column isn't on screen.
    /// </summary>
    public Rect? ColumnFrame(WindowId window)
    {
        if (Strip.Location(window) is not (int column, _)) return null;
        var windows = Strip.Columns[column].Windows;
        Rect? frame = null;
        foreach (var placement in Placements())
        {
            if (!windows.Contains(placement.Window) || placement.Visibility is not (Visibility.Visible or Visibility.Partial)) continue;
            frame = frame?.Union(placement.Frame) ?? placement.Frame;
        }
        if (TabBars().FirstOrDefault(b => b.Column == column) is { } bar) frame = frame?.Union(bar.Frame) ?? bar.Frame;
        return frame is { } shown ? shown.Intersection(Viewport) : null;
    }

    /// <summary>
    /// A slim bar, <paramref name="thickness"/> wide, in the gap where a column inserted at
    /// <paramref name="index"/> would go: for a window dropped onto this strip from another monitor.
    /// </summary>
    public Rect InsertionFrame(int index, double thickness)
    {
        // Tiled, a dropped window takes a tile's place and pushes the rest on: show that tile.
        if (Layout == LayoutMode.Tiled && Tiles() is { Count: > 0 } tiles)
            return TileFrames(tiles)[Math.Clamp(index, 0, tiles.Count - 1)];
        var offsets = ColumnOffsets();
        var edge = index < offsets.Count ? offsets[index] : Strip.IsEmpty ? 0 : ContentWidth + Settings.Gap;
        var x = Viewport.MinX + Settings.Gap + edge - ScrollOffset - Settings.Gap / 2;
        var middle = Math.Clamp(x, Viewport.MinX + thickness / 2, Viewport.MaxX - thickness / 2);
        return new Rect(Rounding.Round(middle - thickness / 2), Viewport.MinY + Settings.Gap, thickness, Math.Max(Viewport.Height - 2 * Settings.Gap, 0));
    }

    /// <summary>A column's left edge on screen, and whether it is visible or parked at an edge.</summary>
    private (double X, Visibility Visibility) Position(double width, double offset)
    {
        var x = Viewport.MinX + Settings.Gap + offset - ScrollOffset;
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
