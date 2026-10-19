using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

/// <summary>The spiral geometry on its own.</summary>
public class TiledGeometryTests
{
    private static readonly Rect Screen = new(0, 0, 1212, 812); // With a 12 px gap the room is 1188 × 788.

    public static TheoryData<int, int> CountsAndColumns
    {
        get
        {
            var data = new TheoryData<int, int>();
            foreach (var columns in new[] { 2, 3 })
                for (var count = 1; count <= 7; count++) data.Add(count, columns);
            return data;
        }
    }

    [Fact]
    public void OneWindowFillsTheRoom() => Assert.Equal([new Rect(12, 12, 1188, 788)], TiledLayout.Frames(Screen, 1, 2, 12));

    [Fact]
    public void NoWindowsNoFrames() => Assert.Empty(TiledLayout.Frames(Screen, 0, 2, 12));

    [Fact]
    public void TwoColumns()
    {
        var frames = TiledLayout.Frames(Screen, 2, 2, 12);
        Assert.Equal([new Rect(12, 12, 588, 788), new Rect(612, 12, 588, 788)], frames);
    }

    [Fact]
    public void TwoColumnsThenTheLastSplitsAlternately()
    {
        Assert.Equal([new Rect(12, 12, 588, 788), new Rect(612, 12, 588, 388), new Rect(612, 412, 588, 388)],
                     TiledLayout.Frames(Screen, 3, 2, 12));
        Assert.Equal([new Rect(12, 12, 588, 788), new Rect(612, 12, 588, 388), new Rect(612, 412, 288, 388), new Rect(912, 412, 288, 388)],
                     TiledLayout.Frames(Screen, 4, 2, 12));
        Assert.Equal(new Rect(912, 412, 288, 188), TiledLayout.Frames(Screen, 5, 2, 12)[3]);
        Assert.Equal(new Rect(912, 612, 288, 188), TiledLayout.Frames(Screen, 5, 2, 12)[4]);
    }

    [Fact]
    public void ThreeColumnsAreThirds()
    {
        var frames = TiledLayout.Frames(Screen, 3, 3, 12);
        Assert.Equal([new Rect(12, 12, 388, 788), new Rect(412, 12, 388, 788), new Rect(812, 12, 388, 788)], frames);
    }

    [Fact]
    public void ThreeColumnsThenTheLastSplits()
    {
        Assert.Equal([new Rect(12, 12, 388, 788), new Rect(412, 12, 388, 788), new Rect(812, 12, 388, 388), new Rect(812, 412, 388, 388)],
                     TiledLayout.Frames(Screen, 4, 3, 12));
        var five = TiledLayout.Frames(Screen, 5, 3, 12);
        Assert.Equal(new Rect(812, 412, 188, 388), five[3]);
        Assert.Equal(new Rect(1012, 412, 188, 388), five[4]);
    }

    [Fact]
    public void TwoWindowsWithThreeColumnsAreHalves() =>
        Assert.Equal(TiledLayout.Frames(Screen, 2, 2, 12), TiledLayout.Frames(Screen, 2, 3, 12));

    [Theory]
    [MemberData(nameof(CountsAndColumns))]
    public void FramesTileTheRoomWithExactGaps(int count, int columns)
    {
        foreach (var (screen, gap) in new[] { (Screen, 12.0), (new Rect(0, 0, 1920, 1080), 12), (new Rect(-1920, 30, 1920, 1050), 13), (new Rect(0, 0, 3440, 1440), 24),
                                              (new Rect(100, 50, 1001, 777), 7), (new Rect(0, 0, 1366, 768), 0), (new Rect(0, 40, 1537, 913), 19) })
        {
            var frames = TiledLayout.Frames(screen, count, columns, gap);
            Assert.Equal(count, frames.Count);
            var room = new Rect(screen.X + gap, screen.Y + gap, screen.Width - 2 * gap, screen.Height - 2 * gap);
            Assert.All(frames, f =>
            {
                Assert.Equal(Math.Round(f.X), f.X);
                Assert.Equal(Math.Round(f.Y), f.Y);
                Assert.Equal(Math.Round(f.Width), f.Width);
                Assert.Equal(Math.Round(f.Height), f.Height);
                Assert.True(f.Width > 0 && f.Height > 0);
                Assert.True(room.Contains(f), $"{f} is outside {room}");
            });
            // The tiles reach every edge of the room.
            Assert.Equal(room.MinX, frames.Min(f => f.MinX));
            Assert.Equal(room.MaxX, frames.Max(f => f.MaxX));
            Assert.Equal(room.MinY, frames.Min(f => f.MinY));
            Assert.Equal(room.MaxY, frames.Max(f => f.MaxY));
            // Nothing overlaps, and whatever faces a tile across a seam is exactly one gap away.
            for (var a = 0; a < count; a++)
            {
                for (var b = a + 1; b < count; b++)
                {
                    var overlapsX = Math.Min(frames[a].MaxX, frames[b].MaxX) - Math.Max(frames[a].MinX, frames[b].MinX) > 0;
                    var overlapsY = Math.Min(frames[a].MaxY, frames[b].MaxY) - Math.Max(frames[a].MinY, frames[b].MinY) > 0;
                    Assert.False(overlapsX && overlapsY, $"{frames[a]} overlaps {frames[b]}");
                }
                foreach (var side in Enum.GetValues<TileSide>())
                {
                    if (TiledLayout.Neighbour(frames, a, side) is not int other) continue;
                    var seam = side switch
                    {
                        TileSide.Left => frames[a].MinX - frames[other].MaxX,
                        TileSide.Right => frames[other].MinX - frames[a].MaxX,
                        TileSide.Up => frames[a].MinY - frames[other].MaxY,
                        _ => frames[other].MinY - frames[a].MaxY,
                    };
                    Assert.Equal(gap, seam);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(CountsAndColumns))]
    public void EqualColumnsDifferByAtMostAPixel(int count, int columns)
    {
        if (count > columns) return;
        var widths = TiledLayout.Frames(new Rect(0, 0, 1921, 1079), count, columns, 12).Select(f => f.Width).ToList();
        Assert.True(widths.Max() - widths.Min() <= 1, string.Join(",", widths));
    }

    [Fact]
    public void RatiosMoveASplitAndBadOnesAreIgnored()
    {
        Assert.Equal(784, TiledLayout.Frames(Screen, 2, 2, 12, [2.0 / 3])[0].Width);
        Assert.Equal(392, TiledLayout.Frames(Screen, 2, 2, 12, [1.0 / 3])[0].Width); // ⅓ of 1176
        var defaults = TiledLayout.Frames(Screen, 4, 2, 12);
        Assert.Equal(defaults, TiledLayout.Frames(Screen, 4, 2, 12, [0, 1.5, double.NaN]));
        Assert.Equal(defaults, TiledLayout.Frames(Screen, 4, 2, 12, []));
        Assert.Equal(defaults, TiledLayout.Frames(Screen, 4, 2, 12, null));
        // Only the third split (3 | 4) changes; the first two keep their defaults.
        var changed = TiledLayout.Frames(Screen, 4, 2, 12, [0, 0, 0.25]);
        Assert.Equal(defaults[0], changed[0]);
        Assert.Equal(defaults[1], changed[1]);
        Assert.Equal(144, changed[2].Width);
        Assert.Equal(432, changed[3].Width);
    }

    [Fact]
    public void DefaultRatiosAndDirections()
    {
        Assert.Equal(1.0 / 3, TiledLayout.DefaultRatio(0, 3, 3));
        Assert.Equal(0.5, TiledLayout.DefaultRatio(1, 3, 3));
        Assert.Equal(0.5, TiledLayout.DefaultRatio(0, 2, 3));
        Assert.Equal(1.0 / 3, TiledLayout.DefaultRatio(0, 6, 3));
        Assert.Equal(0.5, TiledLayout.DefaultRatio(0, 6, 2));
        Assert.Equal(0.5, TiledLayout.DefaultRatio(4, 6, 3));
        // N=2: columns, then rows, columns, rows…; N=3: columns, columns, rows, columns…
        Assert.Equal([true, false, true, false], Enumerable.Range(0, 4).Select(k => TiledLayout.IsSideBySide(k, 2)));
        Assert.Equal([true, true, false, true, false], Enumerable.Range(0, 5).Select(k => TiledLayout.IsSideBySide(k, 3)));
    }

    [Theory]
    [InlineData(1920, 1080, 2)]
    [InlineData(2560, 1440, 2)]
    [InlineData(1366, 768, 2)]
    [InlineData(1080, 1920, 2)]
    [InlineData(3840, 1920, 3)] // Exactly 2:1
    [InlineData(3440, 1440, 3)]
    [InlineData(2560, 1080, 3)]
    [InlineData(5120, 1440, 3)]
    [InlineData(3000, 1501, 2)]
    public void AutoColumnsGoByTheScreensShape(double width, double height, int expected) =>
        Assert.Equal(expected, TiledLayout.AutoColumns(new Rect(0, 0, width, height)));

    [Fact]
    public void NeighboursByGeometry()
    {
        var three = TiledLayout.Frames(Screen, 3, 2, 12); // [0][1 over 2]
        Assert.Equal(1, TiledLayout.Neighbour(three, 0, TileSide.Right)); // Tie between 1 and 2: the earlier
        Assert.Equal(0, TiledLayout.Neighbour(three, 2, TileSide.Left));
        Assert.Equal(1, TiledLayout.Neighbour(three, 2, TileSide.Up));
        Assert.Equal(2, TiledLayout.Neighbour(three, 1, TileSide.Down));
        Assert.Null(TiledLayout.Neighbour(three, 0, TileSide.Left));
        Assert.Null(TiledLayout.Neighbour(three, 1, TileSide.Up));
        Assert.Null(TiledLayout.Neighbour(three, 2, TileSide.Right));
        Assert.Null(TiledLayout.Neighbour(three, 0, TileSide.Down));

        var five = TiledLayout.Frames(Screen, 5, 2, 12); // [0][1 over (2 | (3 over 4))]
        Assert.Equal(2, TiledLayout.Neighbour(five, 3, TileSide.Left));
        Assert.Equal(2, TiledLayout.Neighbour(five, 4, TileSide.Left));
        Assert.Equal(3, TiledLayout.Neighbour(five, 4, TileSide.Up));
        Assert.Equal(1, TiledLayout.Neighbour(five, 2, TileSide.Up));
        Assert.Equal(1, TiledLayout.Neighbour(five, 3, TileSide.Up));
        Assert.Equal(0, TiledLayout.Neighbour(five, 1, TileSide.Left));
        Assert.Null(TiledLayout.Neighbour(five, 2, TileSide.Down));
        Assert.Null(TiledLayout.Neighbour([], 0, TileSide.Left));
    }

    [Fact]
    public void ANeighbourNeedsToOverlapOnTheOtherAxis()
    {
        // Two stacked pairs with different dividers: the short top-right tile only faces the first tile on the left.
        Rect[] frames = [new(0, 0, 100, 50), new(0, 60, 100, 40), new(110, 0, 100, 20), new(110, 30, 100, 70)];
        Assert.Equal(0, TiledLayout.Neighbour(frames, 2, TileSide.Left));
        Assert.Equal(1, TiledLayout.Neighbour(frames, 3, TileSide.Left)); // Tiles 0 and 1 are both one gap away and overlap; 1 is closer in centre
    }
}

public class TiledLayoutTests
{
    private static Workspace Make(int count, int columns = 2, LayoutMode? layout = LayoutMode.Tiled, int focused = 0) =>
        new(new Strip(Enumerable.Range(1, count).Select(i => Col([i])), focused), Viewport) { LayoutOverride = layout, TiledColumnsOverride = columns };

    private static IReadOnlyList<Rect> Expected(Workspace ws, int? count = null, IReadOnlyList<double>? ratios = null) =>
        TiledLayout.Frames(ws.Viewport, count ?? ws.Strip.Windows.Count, ws.TiledColumns, ws.Settings.Gap, ratios);

    // Placement

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(5, 2)]
    [InlineData(3, 3)]
    [InlineData(6, 3)]
    public void PlacementsFollowTheSpiralAndNothingScrollsOrParks(int count, int columns)
    {
        var ws = Make(count, columns);
        var frames = ws.Placements().Select(p => p.Frame).ToList();
        Assert.Equal(Expected(ws), frames);
        Assert.All(ws.Placements(), p => Assert.Equal(Visibility.Visible, p.Visibility));
        Assert.Equal(Enumerable.Range(1, count).Select(i => (long)i), Ids(ws.Placements().Select(p => p.Window)));
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Fact]
    public void ThreeWindowsOnTwoColumns()
    {
        var frames = Frames(Make(3));
        Assert.Equal(new Rect(12, 52, 588, 776), frames[1]);
        Assert.Equal(new Rect(612, 52, 588, 382), frames[2]);
        Assert.Equal(new Rect(612, 446, 588, 382), frames[3]);
    }

    [Fact]
    public void TheColumnCountComesFromTheOverrideThenTheDefaultThenTheScreen()
    {
        var ws = Make(3, columns: 3);
        ws.TiledColumnsOverride = null;
        Assert.Equal(2, ws.TiledColumns); // 1212 × 800 is not ultrawide
        ws.Settings = ws.Settings with { DefaultTiledColumns = 3 };
        Assert.Equal(3, ws.TiledColumns);
        ws.TiledColumnsOverride = 2;
        Assert.Equal(2, ws.TiledColumns);
        ws.TiledColumnsOverride = null;
        ws.Settings = LayoutSettings.Standard;
        ws.Viewport = new Rect(0, 0, 3440, 1440);
        Assert.Equal(3, ws.TiledColumns);
        Assert.Equal<int?>(2, new LayoutSettings(defaultTiledColumns: 1).DefaultTiledColumns); // Clamped to 2…3
        Assert.Equal<int?>(3, new LayoutSettings(defaultTiledColumns: 9).DefaultTiledColumns);
        Assert.Null(new LayoutSettings().DefaultTiledColumns);
    }

    [Fact]
    public void TheDefaultColumnCountAppliesLiveToUntouchedWorkspacesOnly()
    {
        var set = new WorkspaceSet(Viewport);
        set.Workspaces[1].CycleTiledColumns(); // 2 → 3 by hand
        set.Settings = set.Settings with { DefaultTiledColumns = 3 };
        Assert.Equal(3, set.Workspaces[0].TiledColumns);
        set.Settings = set.Settings with { DefaultTiledColumns = 2 };
        Assert.Equal(2, set.Workspaces[0].TiledColumns);
        Assert.Equal(3, set.Workspaces[1].TiledColumns);
    }

    [Fact]
    public void AMinimumWiderThanItsTileTakesItFromTheNeighbour()
    {
        var ws = Make(2);
        ws.MinimumWidths[1] = 800;
        var frames = Frames(ws);
        Assert.Equal(new Rect(12, 52, 800, 776), frames[1]);
        Assert.Equal(new Rect(824, 52, 376, 776), frames[2]); // Shrunk to make room: no overlap
        Assert.Equal(frames[1].MaxX + 12, frames[2].MinX);
    }

    [Fact]
    public void ASecondPartTileShrinksItsSplitThroughTheEarlierOne()
    {
        var ws = Make(3); // 1 | (2 over 3): tiles 2 and 3 both sit in the second part of the first split
        ws.MinimumWidths[3] = 900;
        var frames = Frames(ws);
        Assert.Equal(900, frames[3].Width);
        Assert.Equal(900, frames[2].Width); // The top tile in that part shares its width
        Assert.Equal(276, frames[1].Width); // The first part gets what is left
        Assert.Equal(300, frames[3].MinX);
        Assert.True(frames[1].MaxX + 12 <= frames[3].MinX);
    }

    [Fact]
    public void MinimumsThatCannotFitFallBackToOverlap()
    {
        var ws = Make(2);
        ws.MinimumWidths[1] = 800;
        ws.MinimumWidths[2] = 700; // 1500 px of minimums in 1176 px of room
        var frames = Frames(ws);
        Assert.Equal(new Rect(12, 52, 800, 776), frames[1]); // Overlaps its neighbour
        Assert.Equal(new Rect(500, 52, 700, 776), frames[2]); // Pulled left so it stays on screen
        ws.MinimumWidths[2] = 5000;
        Assert.Equal(ws.UsableWidth, Frames(ws)[2].Width);
    }

    [Fact]
    public void ANeighbourNeverGivesUpItsLastTenPercent()
    {
        var ws = Make(2);
        ws.MinimumWidths[2] = 1100; // Would leave the first tile 76 px, under a tenth of the room
        Assert.Equal(new Rect(100, 52, 1100, 776), Frames(ws)[2]);
        Assert.Equal(new Rect(12, 52, 588, 776), Frames(ws)[1]);
        ws.MinimumWidths[2] = 1000;
        Assert.Equal(new Rect(12, 52, 176, 776), Frames(ws)[1]);
        Assert.Equal(new Rect(200, 52, 1000, 776), Frames(ws)[2]);
    }

    [Fact]
    public void ATabbedColumnIsOneTileWithItsBarAtTheTop()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2, 3, 4], focusedRow: 1, isTabbed: true)]), Viewport) { LayoutOverride = LayoutMode.Tiled };
        var tile = Expected(ws, 2)[1];
        var bars = ws.TabBars();
        var bar = Assert.Single(bars);
        Assert.Equal(1, bar.Column);
        Assert.Equal(new Rect(tile.X, tile.Y, tile.Width, ws.Settings.TabBarHeight), bar.Frame);
        Assert.Equal(3, bar.ActiveWindow);
        var placements = PlacementMap(ws);
        var reserved = ws.Settings.TabBarHeight + ws.Settings.TabBarSpacing;
        Assert.Equal(new Rect(tile.X, tile.Y + reserved, tile.Width, tile.Height - reserved), placements[3].Frame);
        Assert.Equal(Visibility.Visible, placements[3].Visibility);
        Assert.Equal(Visibility.HiddenTab, placements[2].Visibility);
        Assert.Equal(Visibility.HiddenTab, placements[4].Visibility);
        Assert.Equal(ws.HiddenOrigin, placements[2].Frame.Origin);
    }

    [Fact]
    public void AStackThatSneaksInIsPlacedAsTilesAndThenSplitIntoColumns()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2, 3])]), Viewport) { LayoutOverride = LayoutMode.Tiled };
        Assert.Equal(Expected(ws, 3), ws.Placements().Select(p => p.Frame).ToList()); // Already three tiles
        ws.EnsureFocusedVisible();
        Assert.Equal([[1], [2], [3]], Layout(ws.Strip));
    }

    [Fact]
    public void DroppingWindowsOnAColumnKeepsOneTilePerColumn()
    {
        var ws = Make(3, focused: 1);
        ws.Strip.Join(9, 2);
        ws.EnsureFocusedVisible();
        Assert.Equal([[1], [2], [9], [3]], Layout(ws.Strip));
        Assert.Equal(9, ws.Strip.FocusedWindow);
    }

    [Fact]
    public void NewWindowsBecomeTheNextTileAfterTheFocusedOne()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.LayoutOverride = LayoutMode.Tiled;
        foreach (var id in new[] { 1, 2, 3 }) set.Insert(id, 1);
        set.Active.Strip.Focus(1);
        set.Insert(4, 1);
        Assert.Equal([1, 4, 2, 3], Ids(set.Active.Strip.Windows));
        Assert.Equal(0, set.Active.ScrollOffset);
        Assert.Equal(4, Frames(set.Active).Count);
    }

    // Focus and move

    [Fact]
    public void FocusGoesToTheTileInThatDirection()
    {
        var ws = Make(5, focused: 0); // [0][1 over (2 | (3 over 4))]
        Assert.True(ws.Perform(GlideAction.FocusColumnRight));
        Assert.Equal(1, ws.Strip.FocusedColumnIndex);
        Assert.True(ws.Perform(GlideAction.FocusWindowDown));
        Assert.Equal(2, ws.Strip.FocusedColumnIndex);
        Assert.True(ws.Perform(GlideAction.FocusColumnRight));
        Assert.Equal(3, ws.Strip.FocusedColumnIndex);
        Assert.True(ws.Perform(GlideAction.FocusWindowDown));
        Assert.Equal(4, ws.Strip.FocusedColumnIndex);
        Assert.False(ws.Perform(GlideAction.FocusWindowDown));
        Assert.True(ws.Perform(GlideAction.FocusColumnLeft));
        Assert.Equal(2, ws.Strip.FocusedColumnIndex);
        Assert.True(ws.Perform(GlideAction.FocusWindowUp));
        Assert.Equal(1, ws.Strip.FocusedColumnIndex);
        Assert.False(ws.Perform(GlideAction.FocusWindowUp));
        Assert.True(ws.Perform(GlideAction.FocusColumnLeft));
        Assert.Equal(0, ws.Strip.FocusedColumnIndex);
        Assert.False(ws.Perform(GlideAction.FocusColumnLeft));
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Fact]
    public void FocusFirstAndLastAreTheFirstAndLastTiles()
    {
        var ws = Make(4, focused: 1);
        Assert.True(ws.Perform(GlideAction.FocusLastColumn));
        Assert.Equal(3, ws.Strip.FocusedColumnIndex);
        Assert.False(ws.Perform(GlideAction.FocusLastColumn));
        Assert.True(ws.Perform(GlideAction.FocusFirstColumn));
        Assert.Equal(0, ws.Strip.FocusedColumnIndex);
    }

    [Fact]
    public void MoveSwapsWithTheNeighbourAndFocusFollowsTheWindow()
    {
        var ws = Make(3, focused: 0); // [0][1 over 2]
        Assert.True(ws.Perform(GlideAction.MoveColumnRight));
        Assert.Equal([[2], [1], [3]], Layout(ws.Strip));
        Assert.Equal(1, ws.Strip.FocusedWindow);
        Assert.Equal(new Rect(612, 52, 588, 382), Frames(ws)[1]);
        Assert.True(ws.Perform(GlideAction.MoveWindowDown));
        Assert.Equal([[2], [3], [1]], Layout(ws.Strip));
        Assert.Equal(1, ws.Strip.FocusedWindow);
        Assert.True(ws.Perform(GlideAction.MoveColumnLeft));
        Assert.Equal([[1], [3], [2]], Layout(ws.Strip));
        Assert.False(ws.Perform(GlideAction.MoveColumnLeft));
        Assert.False(ws.Perform(GlideAction.MoveWindowUp));
    }

    [Fact]
    public void ConsumeJoinsTheTileOnTheLeftAsATab()
    {
        var ws = Make(3, focused: 1); // [1][2 over 3]
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelLeft));
        Assert.Equal([[1, 2], [3]], Layout(ws.Strip));
        Assert.Equal(2, ws.Strip.FocusedWindow);
        Assert.True(ws.Strip.Columns[0].IsTabbed);
        Assert.Equal(2, ws.Placements().Count(p => p.Visibility == Visibility.Visible)); // Two tiles
        ws.EnsureFocusedVisible();
        Assert.Equal([[1, 2], [3]], Layout(ws.Strip)); // Stays a tab group, not exploded back
        Assert.Equal(Visibility.Visible, PlacementMap(ws)[2].Visibility);
        Assert.Equal(Visibility.HiddenTab, PlacementMap(ws)[1].Visibility);
    }

    [Fact]
    public void ConsumeJoinsTheTileOnTheRightAsTheLastTab()
    {
        var ws = Make(3, focused: 0); // [1][2 over 3]: the right neighbour is the earlier of 2 and 3
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[2, 1], [3]], Layout(ws.Strip));
        Assert.Equal(1, ws.Strip.FocusedWindow);
        Assert.True(ws.Strip.Columns[0].IsTabbed);
        Assert.Equal(Visibility.Visible, PlacementMap(ws)[1].Visibility);
    }

    [Fact]
    public void ConsumeIntoANeighbourThatIsAlreadyTabbed()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2, 3], isTabbed: true), Col([4])], focusedColumnIndex: 0), Viewport) { LayoutOverride = LayoutMode.Tiled };
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[2, 3, 1], [4]], Layout(ws.Strip));
        Assert.True(ws.Strip.Columns[0].IsTabbed);
        Assert.Equal(1, ws.Strip.FocusedWindow);
    }

    [Fact]
    public void ConsumeDoesNothingWithNoTileOnThatSide()
    {
        var ws = Make(3, focused: 1); // Tile 2 (top right) has nothing to its right
        Assert.False(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[1], [2], [3]], Layout(ws.Strip));
        Assert.False(Make(1).Perform(GlideAction.ConsumeOrExpelLeft));
        var alone = Make(1);
        Assert.False(alone.Perform(GlideAction.ConsumeOrExpelLeft));
        Assert.False(alone.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[1]], Layout(alone.Strip));
    }

    [Fact]
    public void ExpelFromATabGroupMakesANewTileOnThatSide()
    {
        var ws = new Workspace(new Strip([Col([1, 2, 3], focusedRow: 1, isTabbed: true), Col([4])]), Viewport) { LayoutOverride = LayoutMode.Tiled };
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[1, 3], [2], [4]], Layout(ws.Strip));
        Assert.Equal(2, ws.Strip.FocusedWindow);
        Assert.Equal(3, ws.Placements().Count(p => p.Visibility == Visibility.Visible));
        Assert.True(ws.Strip.Columns[0].IsTabbed);
        Assert.False(ws.Strip.Columns[1].IsTabbed);
        Assert.Equal(Visibility.Visible, PlacementMap(ws)[2].Visibility);
    }

    [Fact]
    public void ExpelToTheLeftAndConsumeBackRoundTrips()
    {
        var ws = new Workspace(new Strip([Col([1, 2], focusedRow: 1, isTabbed: true), Col([3])], focusedColumnIndex: 0), Viewport) { LayoutOverride = LayoutMode.Tiled };
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.Equal([[1], [2], [3]], Layout(ws.Strip));
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelLeft));
        Assert.Equal([[1, 2], [3]], Layout(ws.Strip));
        Assert.Equal(2, ws.Strip.FocusedWindow);
        Assert.True(ws.Strip.Columns[0].IsTabbed);
    }

    [Fact]
    public void CenterColumnDoesNothingWhileTiled()
    {
        var ws = Make(3, focused: 1);
        Assert.False(ws.Perform(GlideAction.CenterColumn));
        Assert.Equal(0, ws.ScrollOffset);
        Assert.Equal((0, 0), ws.ScrollBounds);
        ws.Scroll(300);
        ws.SettleSwipe(500);
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Fact]
    public void ActionsOnAnEmptyTiledWorkspaceAreHarmless()
    {
        var ws = Make(0);
        foreach (var action in new[] { GlideAction.FocusColumnLeft, GlideAction.MoveColumnRight, GlideAction.CycleColumnWidth, GlideAction.ToggleFullWidth })
            Assert.False(ws.Perform(action));
        Assert.True(ws.Perform(GlideAction.ToggleLayout));
        Assert.Equal(LayoutMode.Scrolling, ws.Layout);
    }

    // Zoom

    [Fact]
    public void FullWidthZoomsTheFocusedTileUntilToggledAgain()
    {
        var ws = Make(3, focused: 1);
        var before = Frames(ws);
        Assert.True(ws.Perform(GlideAction.ToggleFullWidth));
        var zoomed = Frames(ws);
        Assert.Equal(new Rect(12, 52, 1188, 776), zoomed[2]);
        Assert.Equal(before[1], zoomed[1]); // The others stay put, underneath
        Assert.Equal(before[3], zoomed[3]);
        Assert.True(ws.Perform(GlideAction.ToggleFullWidth));
        Assert.Equal(before, Frames(ws));
    }

    [Fact]
    public void AZoomEndsWhenFocusLeavesAndDoesNotComeBack()
    {
        var ws = Make(3, focused: 0);
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.True(ws.Perform(GlideAction.FocusColumnRight));
        Assert.Null(ws.ZoomedWindow);
        ws.Perform(GlideAction.FocusColumnLeft);
        Assert.NotEqual(new Rect(12, 52, 1188, 776), Frames(ws)[1]);
    }

    [Fact]
    public void AZoomEndsWhenItsWindowLeaves()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.LayoutOverride = LayoutMode.Tiled;
        foreach (var id in new[] { 1, 2 }) set.Insert(id, 1);
        set.Active.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(2, set.Active.ZoomedWindow);
        set.Remove(2);
        Assert.Null(set.Active.ZoomedWindow);
        set.Insert(2, 1);
        Assert.Equal(new Rect(612, 52, 588, 776), Frames(set.Active)[2]);
    }

    [Fact]
    public void ATabbedTileCanBeZoomedToo()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2, 3], isTabbed: true)], focusedColumnIndex: 1), Viewport) { LayoutOverride = LayoutMode.Tiled };
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(12, ws.TabBars().Single().Frame.X);
        Assert.Equal(1188, ws.TabBars().Single().Frame.Width);
    }

    // Width: split ratios

    [Fact]
    public void CyclingWidthStepsTheFocusedTilesSplitThroughThirdHalfTwoThirds()
    {
        var ws = Make(2, focused: 0);
        double Width() => Frames(ws)[1].Width;
        Assert.Equal(588, Width());
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Equal(784, Width()); // ⅔
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Equal(392, Width()); // ⅓ (wraps)
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Equal(588, Width());
        Assert.True(ws.Perform(GlideAction.CycleColumnWidthBackward));
        Assert.Equal(392, Width());
        Assert.True(ws.Perform(GlideAction.CycleColumnWidthBackward));
        Assert.Equal(784, Width());
        Assert.Equal(1188, Frames(ws)[2].MaxX - Frames(ws)[1].MinX);
    }

    [Fact]
    public void TheLastTileChangesItsOwnShareThroughTheSplitBeforeIt()
    {
        var ws = Make(2, focused: 1);
        ws.Perform(GlideAction.CycleColumnWidth);
        Assert.Equal(784, Frames(ws)[2].Width); // ⅔ for the tile in focus
        Assert.Equal(392, Frames(ws)[1].Width);
    }

    [Fact]
    public void ThreeColumnsStartAtAThirdAndCycleFromThere()
    {
        var ws = Make(3, columns: 3, focused: 0);
        Assert.Equal(388, Frames(ws)[1].Width);
        ws.Perform(GlideAction.CycleColumnWidth);
        Assert.Equal(588, Frames(ws)[1].Width); // ½ of what is left after the gap
        Assert.Equal(288, Frames(ws)[2].Width); // The other two share the rest
    }

    [Fact]
    public void CyclingATileInTheMiddleOfTheSpiralChangesItsOwnSplit()
    {
        var ws = Make(4, focused: 1); // Tile 1 over (2 | 3): its own split is the row split
        var before = Frames(ws);
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        var after = Frames(ws);
        Assert.Equal(before[1].Width, after[1].Width);
        Assert.True(after[2].Height > before[2].Height); // ⅔ of the height
        Assert.Equal(before[3], Frames(Make(4))[3]);
        Assert.Equal(TiledLayout.Frames(ws.Viewport, 4, 2, 12, [0, 2.0 / 3, 0]), ws.Placements().Select(p => p.Frame).ToList());
    }

    [Fact]
    public void CyclingDoesNothingWithOneTile()
    {
        var ws = Make(1);
        Assert.False(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Null(ws.TiledRatios);
    }

    [Fact]
    public void RatiosStartOverWhenTheWindowCountChanges()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.LayoutOverride = LayoutMode.Tiled;
        set.Insert(1, 1);
        set.Insert(2, 1);
        set.Active.Strip.Focus(1);
        set.Active.Perform(GlideAction.CycleColumnWidth);
        Assert.Equal(784, Frames(set.Active)[1].Width);
        set.Insert(3, 1);
        Assert.Equal(Expected(set.Active), set.Active.Placements().Select(p => p.Frame).ToList());
        set.Remove(3);
        Assert.Equal(784, Frames(set.Active)[1].Width); // Same count again: they are still stored
    }

    // Width: resizing with the mouse

    [Fact]
    public void ResizingAColumnMovesItsSplit()
    {
        var ws = Make(2);
        Assert.True(ws.SetColumnWidthFromResize(1, 784));
        Assert.Equal(784, Frames(ws)[1].Width);
        Assert.Equal(392, Frames(ws)[2].Width);
        Assert.True(ws.SetColumnWidthFromResize(2, 500)); // The second part: the first gets the rest
        Assert.Equal(500, Frames(ws)[2].Width);
        Assert.Equal(676, Frames(ws)[1].Width);
        Assert.False(ws.SetColumnWidthFromResize(2, 500)); // Already so
    }

    [Fact]
    public void ResizingATileInsideAStackedPairChangesTheSplitThatSizesIt()
    {
        var ws = Make(5); // [0][1 over (2 | (3 over 4))]
        Assert.True(ws.SetColumnWidthFromResize(3, 200)); // Tile 2 is the first part of the side-by-side split (2 | …)
        Assert.InRange(Frames(ws)[3].Width, 198, 202);
        Assert.True(ws.SetColumnWidthFromResize(2, 300)); // Tile 1 sits in the second part of the first split
        Assert.InRange(Frames(ws)[2].Width, 298, 302);
        Assert.Equal(12, Frames(ws)[1].MinX);
    }

    [Fact]
    public void ResizeIsClampedAndSomethingAloneSnapsBack()
    {
        var ws = Make(2);
        ws.SetColumnWidthFromResize(1, 5000);
        Assert.InRange(Frames(ws)[1].Width, 100, 1100);
        ws.SetColumnWidthFromResize(1, 1);
        Assert.InRange(Frames(ws)[1].Width, 100, 1100);
        Assert.False(Make(1).SetColumnWidthFromResize(1, 300));
        Assert.False(Make(2).SetColumnWidthFromResize(9, 300));
    }

    [Fact]
    public void ResizeOnTheStripIsTheCustomWidth()
    {
        var ws = Make(2, layout: LayoutMode.Scrolling);
        Assert.True(ws.SetColumnWidthFromResize(1, 700));
        Assert.Equal(700, ws.Width(0));
        Assert.Equal(588, ws.Width(1));
    }

    [Fact]
    public void ResizingPastAMinimumStopsAtItAndNamesTheWindow()
    {
        var ws = Make(2);
        ws.MinimumWidths[1] = 800;
        Assert.True(ws.SetColumnWidthFromResize(1, 500));
        Assert.Equal(800, Frames(ws)[1].Width);
        Assert.Equal(new WindowId(1), ws.MinimumLimit);
        Assert.True(ws.SetColumnWidthFromResize(1, 900));
        Assert.Equal(900, Frames(ws)[1].Width);
        Assert.Null(ws.MinimumLimit);
    }

    [Fact]
    public void CyclingIntoAMinimumStopsAtItAndNamesTheWindow()
    {
        var ws = Make(2);
        ws.MinimumWidths[1] = 800;
        Assert.True(ws.Perform(GlideAction.CycleColumnWidthBackward)); // 1/2 to 1/3 would be 388 px
        Assert.Equal(800, Frames(ws)[1].Width);
        Assert.Equal(new WindowId(1), ws.MinimumLimit);
        Assert.False(ws.Perform(GlideAction.CycleColumnWidthBackward)); // Already at the minimum
        Assert.Equal(new WindowId(1), ws.MinimumLimit);
        ws.MinimumWidths.Clear();
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Null(ws.MinimumLimit);
    }

    // Columns

    [Fact]
    public void CycleTiledColumnsSwitchesBetweenTwoAndThreeAndStartsRatiosOver()
    {
        var ws = Make(4, columns: 2);
        ws.Perform(GlideAction.CycleColumnWidth);
        Assert.NotNull(ws.TiledRatios);
        Assert.True(ws.Perform(GlideAction.CycleTiledColumns));
        Assert.Equal(3, ws.TiledColumns);
        Assert.Equal(3, ws.TiledColumnsOverride);
        Assert.Null(ws.TiledRatios);
        Assert.Equal(Expected(ws), ws.Placements().Select(p => p.Frame).ToList());
        Assert.Equal(new Rect(812, 52, 388, 382), Frames(ws)[3]);
        ws.CycleTiledColumns();
        Assert.Equal(2, ws.TiledColumns);
    }

    [Fact]
    public void CycleTiledColumnsStillSetsTheCountWhileScrolling()
    {
        var ws = Make(3, layout: null);
        var before = ws.Strip.Clone();
        Assert.True(ws.Perform(GlideAction.CycleTiledColumns));
        Assert.Equal(3, ws.TiledColumns);
        Assert.Equal(LayoutMode.Scrolling, ws.Layout);
        Assert.Equal(before, ws.Strip);
        ws.ToggleLayout();
        Assert.Equal(new Rect(812, 52, 388, 776), Frames(ws)[3]);
    }

    // Switching layouts

    [Fact]
    public void ToggleSetsTheOverrideAndFlips()
    {
        var ws = Make(3, layout: null);
        Assert.Null(ws.LayoutOverride);
        Assert.True(ws.Perform(GlideAction.ToggleLayout));
        Assert.Equal(LayoutMode.Tiled, ws.LayoutOverride);
        ws.ToggleLayout();
        Assert.Equal(LayoutMode.Scrolling, ws.LayoutOverride);
    }

    [Fact]
    public void GoingTiledMakesOneColumnPerWindowButKeepsTabsAndFocus()
    {
        var strip = new Strip([Col([1], widthIndex: 0), Col([2, 3, 4], focusedRow: 1, widthIndex: 2, customWidth: 700), Col([5, 6], isTabbed: true), Col([7])], focusedColumnIndex: 1);
        var ws = new Workspace(strip, Viewport);
        ws.ToggleLayout();
        Assert.Equal([[1], [2], [3], [4], [5, 6], [7]], Layout(ws.Strip));
        Assert.Equal(3, ws.Strip.FocusedWindow);
        Assert.True(ws.Strip.Columns[4].IsTabbed);
        Assert.False(ws.Strip.Columns[2].IsTabbed);
        // Each exploded column keeps the stack's width choice.
        Assert.All(new[] { 1, 2, 3 }, i => Assert.Equal(2, ws.Strip.Columns[i].WidthIndex));
        Assert.Equal(0, ws.Strip.Columns[0].WidthIndex);
        Assert.Equal(6, ws.Placements().Count(p => p.Visibility == Visibility.Visible)); // 7 windows, one of them a hidden tab
        Assert.Equal(1, ws.Placements().Count(p => p.Visibility == Visibility.HiddenTab));
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Fact]
    public void GoingBackToScrollingGivesEveryColumnTheDefaultWidth()
    {
        var ws = new Workspace(new Strip([Col([1], widthIndex: 3), Col([2], isFullWidth: true), Col([3], customWidth: 300), Col([4], widthIndex: 0)], focusedColumnIndex: 3), Viewport)
        { LayoutOverride = LayoutMode.Tiled };
        ws.ToggleLayout();
        Assert.Equal(LayoutMode.Scrolling, ws.Layout);
        Assert.All(ws.Strip.Columns, c =>
        {
            Assert.Equal(1, c.WidthIndex);
            Assert.False(c.IsFullWidth);
            Assert.Null(c.CustomWidth);
        });
        Assert.Equal(4, ws.Strip.FocusedWindow);
        Assert.Equal([[1], [2], [3], [4]], Layout(ws.Strip));
        // Four half-width columns don't fit, so the focused one is scrolled into view.
        Assert.True(ws.ScrollOffset > 0);
        Assert.Equal(Visibility.Visible, Visibilities(ws)[4]);
    }

    [Fact]
    public void AZoomIsForgottenWhenReturningToScrolling()
    {
        var ws = Make(2);
        ws.Perform(GlideAction.ToggleFullWidth);
        ws.ToggleLayout();
        ws.ToggleLayout();
        Assert.Null(ws.ZoomedWindow);
    }

    [Fact]
    public void ThereAndBackKeepsTheOrderAndTheFocus()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2, 3]), Col([4, 5], isTabbed: true), Col([6])], focusedColumnIndex: 2), Viewport);
        var order = Ids(ws.Strip.Windows);
        var focus = ws.Strip.FocusedWindow;
        ws.ToggleLayout();
        Assert.Equal(order, Ids(ws.Strip.Windows));
        Assert.Equal(focus, ws.Strip.FocusedWindow);
        ws.ToggleLayout();
        Assert.Equal(order, Ids(ws.Strip.Windows));
        Assert.Equal(focus, ws.Strip.FocusedWindow);
        Assert.Equal([[1], [2], [3], [4, 5], [6]], Layout(ws.Strip));
    }

    [Fact]
    public void ScrollingPlacementsAreUnchanged()
    {
        var ws = Make(3, layout: null);
        Assert.Equal(LayoutMode.Scrolling, ws.Layout);
        Assert.Equal(588, ws.Width(0));
        Assert.Equal(588, Frames(ws)[1].Width);
        Assert.Equal(612, Frames(ws)[2].X);
    }

    [Fact]
    public void DefaultLayoutAppliesLiveToUntouchedWorkspacesOnly()
    {
        var set = new WorkspaceSet(Viewport);
        set.Workspaces[1].ToggleLayout(); // tiled by hand
        set.Settings = set.Settings with { DefaultLayout = LayoutMode.Tiled };
        Assert.Equal(LayoutMode.Tiled, set.Workspaces[0].Layout);
        set.Workspaces[1].ToggleLayout(); // by hand back to scrolling
        Assert.Equal(LayoutMode.Scrolling, set.Workspaces[1].Layout);
        set.Settings = set.Settings with { DefaultLayout = LayoutMode.Scrolling };
        Assert.Equal(LayoutMode.Scrolling, set.Workspaces[0].Layout);
        Assert.Equal(LayoutMode.Scrolling, set.Workspaces[1].Layout);
    }

    [Fact]
    public void SendingAWindowAwayLeavesBothLayoutsAlone()
    {
        var set = new WorkspaceSet(Viewport);
        set.Insert(1, 1);
        set.Active.ToggleLayout();
        set.MoveFocusedWindow(1);
        Assert.Equal(LayoutMode.Tiled, set.Workspaces[0].LayoutOverride);
        Assert.Null(set.Workspaces[1].LayoutOverride);
    }

    // Dropping windows

    [Fact]
    public void ADropOnATiledWorkspaceHighlightsTheTileItWouldTake()
    {
        var ws = Make(3);
        var frames = Expected(ws);
        Assert.Equal(frames[0], ws.InsertionFrame(0, 4));
        Assert.Equal(frames[2], ws.InsertionFrame(2, 4));
        Assert.Equal(frames[2], ws.InsertionFrame(3, 4)); // Past the end: the last tile
    }

    [Fact]
    public void TheDropIndexGoesByTilePositions()
    {
        var ws = Make(3);
        Assert.Equal(0, MonitorGeometry.DropIndex(ws, 50));
        Assert.Equal(1, MonitorGeometry.DropIndex(ws, 400));
        Assert.Equal(3, MonitorGeometry.DropIndex(ws, 1150));
    }

    [Fact]
    public void TheColumnFrameOfATiledWindowIsItsTile()
    {
        var ws = Make(3);
        Assert.Equal(Expected(ws)[1], ws.ColumnFrame(2));
    }

    // Persistence

    [Fact]
    public void SnapshotKeepsLayoutColumnsAndRatios()
    {
        var set = new WorkspaceSet(Viewport);
        set.Insert(1, 1);
        set.Insert(2, 1);
        set.Workspaces[0].ToggleLayout();
        set.Workspaces[0].CycleTiledColumns();
        set.Workspaces[0].Perform(GlideAction.CycleColumnWidth);
        set.Workspaces[2].LayoutOverride = LayoutMode.Scrolling;
        set.Workspaces[3].TiledColumnsOverride = 2;
        var json = new StripSnapshot(set, [], [], DateTimeOffset.UnixEpoch).ToJson();
        Assert.Contains("\"Tiled\"", json);
        var decoded = StripSnapshot.FromJson(json)!;
        Assert.Equal(LayoutMode.Tiled, decoded.Layouts[0]);
        Assert.Null(decoded.Layouts[1]);
        Assert.Equal(LayoutMode.Scrolling, decoded.Layouts[2]);
        Assert.Equal<int?[]>([3, null, null, 2, null, null, null, null, null], [.. decoded.TiledColumns]);
        Assert.NotNull(decoded.TiledRatios[0]);
        Assert.Null(decoded.TiledRatios[1]);
        Assert.Equal(new StripSnapshot(set, [], [], DateTimeOffset.UnixEpoch), decoded);

        var restored = new WorkspaceSet(Viewport);
        var (strips, active) = decoded.RestoreWorkspaces(W(1, 2), 1, 4);
        restored.Restore(strips, active, decoded.Layouts, decoded.TiledColumns, decoded.TiledRatios);
        Assert.Equal(LayoutMode.Tiled, restored.Workspaces[0].LayoutOverride);
        Assert.Equal(3, restored.Workspaces[0].TiledColumnsOverride);
        Assert.Equal(set.Workspaces[0].TiledRatios, restored.Workspaces[0].TiledRatios);
        Assert.Equal(Frames(set.Workspaces[0]), Frames(restored.Workspaces[0]));
        Assert.Null(restored.Workspaces[1].LayoutOverride);
        Assert.Null(restored.Workspaces[1].TiledColumnsOverride);
        Assert.Null(restored.Workspaces[1].TiledRatios);
        Assert.Equal(LayoutMode.Scrolling, restored.Workspaces[2].LayoutOverride);
        Assert.Equal(2, restored.Workspaces[3].TiledColumnsOverride);
    }

    [Fact]
    public void SnapshotsWithoutTheNewFieldsStillLoad()
    {
        const string json = """{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}],"workspaces":[{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}],"layout":"Tiled"}],"userFloating":[],"userTiled":[],"bootTime":"1970-01-01T00:00:00Z"}""";
        var decoded = StripSnapshot.FromJson(json)!;
        Assert.Equal<LayoutMode?[]>([LayoutMode.Tiled], [.. decoded.Layouts]);
        Assert.Equal<int?[]>([null], [.. decoded.TiledColumns]);
        Assert.Null(decoded.TiledRatios[0]);
        const string older = """{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}],"workspaces":[{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}]}],"userFloating":[],"userTiled":[],"bootTime":"1970-01-01T00:00:00Z"}""";
        Assert.Equal<LayoutMode?[]>([null], [.. StripSnapshot.FromJson(older)!.Layouts]);
    }

    // The shortcut

    [Fact]
    public void ToggleLayoutIsAWorkspaceLevelActionWithItsOwnShortcut()
    {
        Assert.Null(GlideAction.ToggleLayout.WorkspaceCommand());
        Assert.Equal(ActionGroup.Workspaces, GlideAction.ToggleLayout.Group());
        Assert.Equal("Win+Ctrl+T", Keymap.Defaults[GlideAction.ToggleLayout].ToString());
        Assert.Equal(GlideAction.ToggleLayout, Keymap.Defaults.Single(p => p.Value.ToString() == "Win+Ctrl+T").Key);
        Assert.Equal(GlideAction.ToggleLayout, Enum.Parse<GlideAction>("ToggleLayout"));
        Assert.Equal(GlideAction.ToggleTiling, Enum.Parse<GlideAction>("ToggleTiling"));
        Assert.Equal("Win+Ctrl+Shift+T", Keymap.Defaults[GlideAction.ToggleTiling].ToString());
    }

    [Fact]
    public void CycleTiledColumnsIsLastInTheEnumHasATitleAndAFreeSafeShortcut()
    {
        Assert.Equal(GlideAction.CycleTiledColumns, GlideActions.All.Max());
        Assert.Equal(GlideAction.CycleTiledColumns, Enum.Parse<GlideAction>("CycleTiledColumns"));
        Assert.Equal("Switch Between 2 and 3 Columns", GlideAction.CycleTiledColumns.Title());
        Assert.Equal(ActionGroup.Workspaces, GlideAction.CycleTiledColumns.Group());
        Assert.Null(GlideAction.CycleTiledColumns.WorkspaceCommand());
        var combo = Keymap.Defaults[GlideAction.CycleTiledColumns];
        Assert.Equal("Win+Ctrl+G", combo.ToString());
        Assert.Single(Keymap.Defaults, p => p.Value == combo);
        Assert.Null(ShortcutPolicy.ReplacedWindowsFeature(combo));
        Assert.IsType<ShortcutPolicy.Verdict.Ok>(ShortcutPolicy.Evaluate(combo));
        Assert.Equal(GlideAction.CycleTiledColumns, new Keymap().Action(combo));
    }

    [Fact]
    public void WindowMovesHaveTheirOwnTitles()
    {
        Assert.Equal("Move Window to Workspace 3", GlideAction.MoveColumnToWorkspace3.Title());
        Assert.Equal("Move Window to Previous Workspace", GlideAction.MoveColumnToPreviousWorkspace.Title());
        Assert.Equal("Move Window to Next Workspace", GlideAction.MoveColumnToNextWorkspace.Title());
        Assert.Equal("Move Column to Monitor Left", GlideAction.MoveColumnToMonitorLeft.Title());
    }
}

/// <summary>The strip operations the tiled conversions use.</summary>
public class StripConversionTests
{
    [Fact]
    public void ExplodeStacksSplitsStacksOnlyAndKeepsFocusOnTheSameWindow()
    {
        var strip = new Strip([Col([1, 2], focusedRow: 1, widthIndex: 2), Col([3, 4], isTabbed: true), Col([5])], focusedColumnIndex: 0);
        Assert.True(strip.ExplodeStacks());
        Assert.Equal([[1], [2], [3, 4], [5]], Layout(strip));
        Assert.Equal(2, strip.FocusedWindow);
        Assert.Equal(1, strip.FocusedColumnIndex);
        Assert.Equal(2, strip.Columns[1].WidthIndex);
        Assert.False(strip.ExplodeStacks());
    }

    [Fact]
    public void SwapColumnsMovesFocusWithTheFocusedOne()
    {
        var strip = new Strip([Col([1]), Col([2]), Col([3])], focusedColumnIndex: 0);
        Assert.True(strip.SwapColumns(0, 2));
        Assert.Equal([[3], [2], [1]], Layout(strip));
        Assert.Equal(1, strip.FocusedWindow);
        Assert.False(strip.SwapColumns(1, 1));
        Assert.False(strip.SwapColumns(0, 5));
        strip.SwapColumns(0, 1); // Neither is focused (1 is at index 2)
        Assert.Equal(1, strip.FocusedWindow);
    }

    [Fact]
    public void RemoveFocusedWindowTakesOnlyThatWindow()
    {
        var strip = new Strip([Col([1], widthIndex: 3), Col([2, 3], focusedRow: 0, widthIndex: 2)], focusedColumnIndex: 1);
        var taken = strip.RemoveFocusedWindow(1)!;
        Assert.Equal([2], Ids(taken.Windows));
        Assert.Equal(1, taken.WidthIndex);
        Assert.Equal([[1], [3]], Layout(strip));
        Assert.Equal(2, strip.Columns[1].WidthIndex);
        strip.Focus(1);
        var alone = strip.RemoveFocusedWindow(1)!;
        Assert.Equal(3, alone.WidthIndex); // Alone in its column: the column goes along
        Assert.Equal([[3]], Layout(strip));
        Assert.Null(new Strip().RemoveFocusedWindow(1));
    }

    [Fact]
    public void ResetWidthsDropsCustomAndFullWidths()
    {
        var strip = new Strip([Col([1], widthIndex: 3, isFullWidth: true), Col([2], customWidth: 400)]);
        strip.ResetWidths(1);
        Assert.Equal([Col([1]), Col([2])], strip.Columns);
    }
}
