using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class WorkspaceTests
{
    private static readonly Rect viewport = Viewport;

    private static Workspace Make(long[][] columns, int[]? widths = null, int focused = 0) =>
        new(new Strip(columns.Select((windows, index) => Col(windows, widthIndex: widths?[index] ?? 1)), focused), viewport);

    [Fact]
    public void PresetWidths()
    {
        var ws = Make([[1], [2], [3], [4]], widths: [0, 1, 2, 3]);
        Assert.Equal(388, ws.Width(0));
        Assert.Equal(588, ws.Width(1));
        Assert.Equal(788, ws.Width(2));
        Assert.Equal(1188, ws.Width(3));
    }

    [Fact]
    public void TwoHalvesFillViewportWithGaps()
    {
        var f = Frames(Make([[1], [2]]));
        Assert.Equal(new Rect(12, 52, 588, 776), f[1]);
        Assert.Equal(new Rect(612, 52, 588, 776), f[2]);
        Assert.Equal(viewport.MaxX, f[2].MaxX + 12);
    }

    [Fact]
    public void CustomGapsStillFillViewport()
    {
        foreach (var gap in new double[] { 0, 24 })
        {
            var ws = Make([[1], [2, 3]]);
            ws.Settings = ws.Settings with { Gap = gap };
            var f = Frames(ws);
            Assert.Equal(gap, f[1].MinX);
            Assert.Equal(gap, f[2].MinX - f[1].MaxX);
            Assert.Equal(viewport.MaxX, f[2].MaxX + gap);
            Assert.Equal(gap, f[3].MinY - f[2].MaxY); // Stacked windows use the same gap
            Assert.Equal(viewport.MaxY, f[3].MaxY + gap, 1.0);
        }
    }

    [Fact]
    public void ThreeThirdsFillViewport()
    {
        var ws = Make([[1], [2], [3]], widths: [0, 0, 0]);
        Assert.Equal(ws.UsableWidth, ws.ContentWidth);
        Assert.All(Visibilities(ws).Values, v => Assert.Equal(Visibility.Visible, v));
    }

    [Fact]
    public void FullWidthOverridesPreset()
    {
        var ws = Make([[1]], widths: [0]);
        ws.Strip.ToggleFullWidth();
        Assert.Equal(1188, ws.Width(0));
    }

    [Fact]
    public void StackedWindowsSplitHeight()
    {
        var f = Frames(Make([[1, 2]]));
        Assert.Equal(new Rect(12, 52, 588, 382), f[1]);
        Assert.Equal(new Rect(12, 446, 588, 382), f[2]);
    }

    [Fact]
    public void MinimumWidthWidensColumnButNotPastViewport()
    {
        var ws = Make([[1, 2]], widths: [0]);
        ws.MinimumWidths[2] = 500;
        Assert.Equal(500, ws.Width(0));
        ws.MinimumWidths[2] = 5000;
        Assert.Equal(1188, ws.Width(0));
    }

    [Fact]
    public void FocusRightScrollsMinimallyToRevealColumn()
    {
        var ws = Make([[1], [2], [3]]);
        Assert.True(ws.Perform(GlideAction.FocusColumnRight));
        Assert.Equal(0, ws.ScrollOffset); // Second half column already fits
        Assert.True(ws.Perform(GlideAction.FocusColumnRight));
        Assert.Equal(600, ws.ScrollOffset); // Third column's right edge aligns with the usable area
        Assert.Equal(viewport.MaxX - 12, Frames(ws)[3].MaxX);
        var v = Visibilities(ws);
        Assert.Equal(Visibility.Visible, v[3]);
        Assert.Equal(Visibility.Visible, v[2]);
        Assert.Equal(Visibility.ParkedLeft, v[1]);
    }

    [Fact]
    public void FocusLeftScrollsBack()
    {
        var ws = Make([[1], [2], [3]], focused: 0);
        ws.Perform(GlideAction.FocusLastColumn);
        ws.Perform(GlideAction.FocusFirstColumn);
        Assert.Equal(0, ws.ScrollOffset);
        Assert.Equal(Visibility.ParkedRight, Visibilities(ws)[3]);
    }

    [Fact]
    public void ParkedColumnsLeaveSliverAtEdges()
    {
        var ws = Make([[1], [2], [3], [4], [5]]);
        ws.Perform(GlideAction.FocusLastColumn);
        ws.Perform(GlideAction.FocusColumnLeft);
        ws.Perform(GlideAction.FocusColumnLeft);
        var f = Frames(ws);
        var v = Visibilities(ws);
        Assert.Equal(Visibility.ParkedLeft, v[1]);
        Assert.Equal(viewport.MinX + ws.Settings.Sliver, f[1].MaxX);
        Assert.Equal(Visibility.Visible, v[3]);
        Assert.Equal(Visibility.Visible, v[4]);
        Assert.Equal(Visibility.ParkedRight, v[5]);
        Assert.Equal(viewport.MaxX - ws.Settings.Sliver, f[5].MinX);
    }

    [Fact]
    public void PartiallyVisibleColumnKeepsRealPosition()
    {
        var ws = Make([[1], [2], [3]], widths: [2, 2, 2]);
        ws.Perform(GlideAction.FocusColumnRight);
        // ⅔ columns: 788 wide. Focusing the second scrolls so it ends at the right edge.
        Assert.Equal(800 + 788 - 1188, ws.ScrollOffset);
        Assert.Equal(Visibility.Partial, Visibilities(ws)[1]);
        Assert.Equal(12 + 0 - 400, Frames(ws)[1].MinX);
    }

    [Fact]
    public void CenterColumn()
    {
        var ws = Make([[1], [2], [3]], widths: [0, 0, 0], focused: 0);
        Assert.True(ws.Perform(GlideAction.CenterColumn));
        Assert.Equal(viewport.MidX, Frames(ws)[1].MidX, 1.0);
        Assert.False(ws.Perform(GlideAction.CenterColumn)); // Already centered
    }

    [Fact]
    public void FocusedColumnAlwaysFullyVisibleAfterEveryAction()
    {
        var ws = Make([[1], [2, 3], [4], [5], [6]], widths: [0, 1, 2, 3, 1]);
        GlideAction[] actions =
        [
            GlideAction.FocusColumnRight, GlideAction.CycleColumnWidth, GlideAction.FocusColumnRight, GlideAction.ToggleFullWidth,
            GlideAction.FocusColumnRight, GlideAction.MoveColumnLeft, GlideAction.FocusLastColumn, GlideAction.ConsumeOrExpelLeft,
            GlideAction.FocusWindowUp, GlideAction.ConsumeOrExpelRight, GlideAction.FocusFirstColumn, GlideAction.MoveColumnRight,
            GlideAction.MoveColumnRight, GlideAction.CycleColumnWidthBackward, GlideAction.FocusColumnLeft,
        ];
        foreach (var action in actions)
        {
            ws.Perform(action);
            var focused = ws.Strip.FocusedWindow!.Value;
            var placement = ws.Placements().First(p => p.Window == focused);
            Assert.True(placement.Visibility == Visibility.Visible, $"after {action}");
            Assert.True(placement.Frame.MinX >= viewport.MinX, $"after {action}");
            Assert.True(placement.Frame.MaxX <= viewport.MaxX, $"after {action}");
        }
    }

    [Fact]
    public void SettleScrollRemovesTrailingSpaceAfterRemoval()
    {
        var ws = Make([[1], [2], [3]]);
        ws.Perform(GlideAction.FocusLastColumn);
        Assert.Equal(600, ws.ScrollOffset);
        ws.Strip.Remove(3);
        ws.SettleScroll();
        Assert.Equal(0, ws.ScrollOffset);
        Assert.All(Visibilities(ws).Values, v => Assert.Equal(Visibility.Visible, v));
    }

    [Fact]
    public void ViewportOriginIsRespected()
    {
        var ws = Make([[1]]);
        ws.Viewport = new Rect(1512, 25, 1212, 800);
        Assert.Equal(new Rect(1524, 37, 588, 776), Frames(ws)[1]);
    }

    [Fact]
    public void NonLayoutActionsReportNoChange()
    {
        var ws = Make([[1]]);
        Assert.False(ws.Perform(GlideAction.ToggleFloating));
        Assert.False(ws.Perform(GlideAction.ToggleTiling));
    }

    [Fact]
    public void EmptyWorkspace()
    {
        var ws = Make([]);
        Assert.Equal(0, ws.ContentWidth);
        Assert.Empty(ws.Placements());
        Assert.False(ws.Perform(GlideAction.FocusColumnRight));
        ws.SettleScroll();
        Assert.Equal(0, ws.ScrollOffset);
    }
}

public class CustomWidthTests
{
    /// <summary>Two ½ columns; presets are 388, 588, 788 and 1188 wide.</summary>
    private static Workspace Make() => new(new Strip([Col([1]), Col([2])]), Viewport);

    [Fact]
    public void CustomWidthReplacesThePreset()
    {
        var ws = Make();
        Assert.True(ws.Strip.SetCustomWidth(700, 1));
        Assert.Equal(700, ws.Width(0));
        Assert.Equal(12 + 700 + 12, Frames(ws)[2].MinX); // Later columns shift along
        ws.Strip.SetCustomWidth(5000, 1);
        Assert.Equal(ws.UsableWidth, ws.Width(0)); // Never wider than the screen
    }

    [Fact]
    public void CyclingFromACustomWidthGoesToTheNextPresetThatWay()
    {
        var ws = Make();
        ws.Strip.SetCustomWidth(700, 1);
        Assert.True(ws.Perform(GlideAction.CycleColumnWidth));
        Assert.Equal(788, ws.Width(0));
        Assert.Null(ws.Strip.Columns[0].CustomWidth);

        ws.Strip.SetCustomWidth(700, 1);
        ws.Perform(GlideAction.CycleColumnWidthBackward);
        Assert.Equal(588, ws.Width(0));

        ws.Strip.SetCustomWidth(1190, 1);
        ws.Perform(GlideAction.CycleColumnWidth);
        Assert.Equal(388, ws.Width(0)); // Wraps past the widest preset
    }

    [Fact]
    public void FullWidthOverridesAndRestoresTheCustomWidth()
    {
        var ws = Make();
        ws.Strip.SetCustomWidth(700, 1);
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(1188, ws.Width(0));
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(700, ws.Width(0));
    }

    [Fact]
    public void CustomWidthSurvivesSnapshotsAndHiding()
    {
        var set = new WorkspaceSet(Viewport);
        set.Active.Strip = Make().Strip;
        set.Active.Strip.SetCustomWidth(640, 2);
        var snapshot = new StripSnapshot(set, [], [], DateTimeOffset.UnixEpoch);
        var decoded = RoundTrip(snapshot);
        Assert.Equal(640, decoded.Restore(W(1, 2), widthIndex: 1, presetCount: 4).Columns[1].CustomWidth);

        var departure = set.DepartureOf(2)!;
        set.Remove(2);
        set.Rejoin(2, departure, focus: false);
        Assert.Equal(640, set.Active.Strip.Columns[1].CustomWidth);
    }
}
