namespace Glide.Core;

/// <summary>Which way to look from a tile for its neighbour.</summary>
public enum TileSide { Left, Right, Up, Down }

/// <summary>
/// The tiled layout's geometry, from counts alone. Tiles are placed in order like a dwindle: the
/// first <c>columns - 1</c> tiles each take a column, then the last column's room is split
/// again and again for the rest, top/bottom first and then left/right, alternating. Split
/// <c>k</c> is the one that cuts tile <c>k</c> off from the room left for the tiles after it.
/// </summary>
public static class TiledLayout
{
    public const int MinColumns = 2;
    public const int MaxColumns = 3;

    /// <summary>The share of a split's room the cycling shortcut steps through for the first tile.</summary>
    public static IReadOnlyList<double> RatioPresets { get; } = [1.0 / 3, 0.5, 2.0 / 3];

    public static int ClampColumns(int columns) => Math.Clamp(columns, MinColumns, MaxColumns);

    /// <summary>Three columns on ultrawide screens (at least twice as wide as tall), else two.</summary>
    public static int AutoColumns(Rect viewport) => viewport.Height > 0 && viewport.Width / viewport.Height >= 2.0 ? MaxColumns : MinColumns;

    /// <summary>Whether split <paramref name="split"/> puts its two parts side by side (a column split) rather than one above the other.</summary>
    public static bool IsSideBySide(int split, int columns) =>
        split < ClampColumns(columns) - 1 || (split - (ClampColumns(columns) - 1)) % 2 == 1;

    /// <summary>
    /// What the first part of a split gets by default, nominally: an equal share of the columns still
    /// to place (a third, then a half, with three columns), else half. Laying out gives the columns
    /// exactly equal widths, gaps taken out; see <see cref="Frames"/>.
    /// </summary>
    public static double DefaultRatio(int split, int count, int columns)
    {
        var levels = Math.Min(count, ClampColumns(columns));
        return split < levels - 1 ? 1.0 / (levels - split) : 0.5;
    }

    /// <summary>Whether <paramref name="ratios"/> holds a usable ratio for a split, rather than leaving it at its default.</summary>
    public static bool IsSet(int split, IReadOnlyList<double>? ratios) => ratios is not null && split < ratios.Count && ratios[split] is > 0 and < 1;

    /// <summary>A split's ratio: the one in <paramref name="ratios"/> if it holds a usable value for it, else the default.</summary>
    public static double Ratio(int split, int count, int columns, IReadOnlyList<double>? ratios) =>
        IsSet(split, ratios) ? ratios![split] : DefaultRatio(split, count, columns);

    /// <summary>
    /// The room each split divides, in order. <paramref name="viewport"/> gets <paramref name="gap"/>
    /// all around its edge, like the strip.
    /// </summary>
    public static IReadOnlyList<(Rect Room, bool SideBySide)> Splits(Rect viewport, int count, int columns, double gap, IReadOnlyList<double>? ratios) =>
        Walk(viewport, count, columns, gap, ratios).Splits;

    /// <summary>
    /// One frame per tile, in order. Whole pixels, with exactly <paramref name="gap"/> between
    /// neighbours and around the edge, and no overlap. <paramref name="ratios"/> sets what the first
    /// part of each split gets, as a share of the room less one gap (see <see cref="Ratio"/>); splits it
    /// doesn't cover, or holds 0 for, take their default: equal columns, else half.
    /// </summary>
    public static IReadOnlyList<Rect> Frames(Rect viewport, int count, int columns, double gap, IReadOnlyList<double>? ratios = null) =>
        Walk(viewport, count, columns, gap, ratios).Frames;

    private static (List<Rect> Frames, List<(Rect Room, bool SideBySide)> Splits) Walk(Rect viewport, int count, int columns, double gap, IReadOnlyList<double>? ratios)
    {
        var frames = new List<Rect>();
        var splits = new List<(Rect, bool)>();
        if (count <= 0) return (frames, splits);
        gap = Rounding.Round(gap);
        columns = ClampColumns(columns);
        var room = new Rect(Rounding.Round(viewport.X) + gap, Rounding.Round(viewport.Y) + gap,
                            Math.Max(Rounding.Round(viewport.Width) - 2 * gap, 0), Math.Max(Rounding.Round(viewport.Height) - 2 * gap, 0));

        for (var split = 0; split < count - 1; split++)
        {
            var sideBySide = IsSideBySide(split, columns);
            splits.Add((room, sideBySide));
            var length = sideBySide ? room.Width : room.Height;
            var available = Math.Max(length - gap, 0);
            var first = Math.Clamp(Rounding.Round(FirstPart(split, count, columns, length, available, gap, ratios)), 0, available);
            var rest = available - first;
            if (sideBySide)
            {
                frames.Add(new Rect(room.X, room.Y, first, room.Height));
                room = new Rect(room.X + first + gap, room.Y, rest, room.Height);
            }
            else
            {
                frames.Add(new Rect(room.X, room.Y, room.Width, first));
                room = new Rect(room.X, room.Y + first + gap, room.Width, rest);
            }
        }
        frames.Add(room);
        return (frames, splits);
    }

    /// <summary>The size of a split's first part: the set ratio of the room, else an equal share of the columns left, else half.</summary>
    private static double FirstPart(int split, int count, int columns, double length, double available, double gap, IReadOnlyList<double>? ratios)
    {
        if (IsSet(split, ratios)) return available * ratios![split];
        var left = Math.Min(count, columns) - split;
        return left > 1 ? (length - (left - 1) * gap) / left : available / 2;
    }

    /// <summary>
    /// The tile next to <paramref name="from"/> on a side: among those whose edge faces it and that
    /// overlap its span along the other axis, the nearest edge; ties go to the one whose centre is closest along the other axis, then
    /// the earlier tile. Null when nothing is that way.
    /// </summary>
    public static int? Neighbour(IReadOnlyList<Rect> frames, int from, TileSide side)
    {
        if (from < 0 || from >= frames.Count) return null;
        var source = frames[from];
        int? best = null;
        (double Edge, double Centre) bestKey = default;
        for (var index = 0; index < frames.Count; index++)
        {
            if (index == from) continue;
            var other = frames[index];
            var (distance, overlap) = side switch
            {
                TileSide.Left => (source.MinX - other.MaxX, Overlap(source.MinY, source.MaxY, other.MinY, other.MaxY)),
                TileSide.Right => (other.MinX - source.MaxX, Overlap(source.MinY, source.MaxY, other.MinY, other.MaxY)),
                TileSide.Up => (source.MinY - other.MaxY, Overlap(source.MinX, source.MaxX, other.MinX, other.MaxX)),
                _ => (other.MinY - source.MaxY, Overlap(source.MinX, source.MaxX, other.MinX, other.MaxX)),
            };
            if (distance < -0.5 || overlap <= 0) continue;
            // Centres are compared along the other axis, to within a pixel, so a near tie goes to the earlier tile.
            var offCentre = side is TileSide.Left or TileSide.Right ? Math.Abs(other.MidY - source.MidY) : Math.Abs(other.MidX - source.MidX);
            var key = (Math.Max(distance, 0), offCentre);
            if (best is null || key.Item1 < bestKey.Edge - 0.5 || (Math.Abs(key.Item1 - bestKey.Edge) <= 0.5 && key.Item2 < bestKey.Centre - 1))
            {
                best = index;
                bestKey = key;
            }
        }
        return best;
    }

    private static double Overlap(double aMin, double aMax, double bMin, double bMax) => Math.Min(aMax, bMax) - Math.Max(aMin, bMin);
}
