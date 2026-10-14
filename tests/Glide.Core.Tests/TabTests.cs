using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class TabTests
{
    private static readonly Rect viewport = Viewport;

    /// <summary>A tabbed column holding windows 1, 2 and 3, next to a plain column with window 4.</summary>
    private static Workspace Make(int focusedRow = 0) =>
        new(new Strip([Col([1, 2, 3], focusedRow: focusedRow, isTabbed: true), Col([4])]), viewport);

    [Fact]
    public void ToggleTabbed()
    {
        var strip = new Strip([Col([1, 2])]);
        Assert.True(strip.ToggleTabbed());
        Assert.True(strip.Columns[0].IsTabbed);
        Assert.True(strip.ToggleTabbed());
        Assert.False(strip.Columns[0].IsTabbed);
        Assert.False(new Strip().ToggleTabbed());
    }

    [Fact]
    public void OnlyTheActiveTabIsShownBelowTheTabBar()
    {
        var p = PlacementMap(Make(focusedRow: 1));
        // 28 px bar + 4 px spacing below the 12 px gap; full column height otherwise.
        Assert.Equal(new Rect(12, 84, 588, 744), p[2].Frame);
        Assert.Equal(Visibility.Visible, p[2].Visibility);
        foreach (WindowId hidden in W(1, 3))
        {
            Assert.Equal(Visibility.HiddenTab, p[hidden].Visibility);
            Assert.Equal(new Point(viewport.MaxX - 1, viewport.MaxY - 1), p[hidden].Frame.Origin);
            Assert.Equal(p[2].Frame.Size, p[hidden].Frame.Size);
        }
        Assert.Equal(new Rect(612, 52, 588, 776), p[4].Frame); // Plain columns are unaffected
    }

    [Fact]
    public void TabBarSitsAboveTheActiveTab()
    {
        var bars = Make(focusedRow: 2).TabBars();
        Assert.Single(bars);
        Assert.Equal(new Rect(12, 52, 588, 28), bars[0].Frame);
        Assert.Equal([1, 2, 3], Ids(bars[0].Windows));
        Assert.Equal(3, bars[0].ActiveWindow);
    }

    [Fact]
    public void FocusUpAndDownSwitchTabs()
    {
        var ws = Make();
        Assert.True(ws.Perform(GlideAction.FocusWindowDown));
        Assert.Equal(Visibility.Visible, PlacementMap(ws)[2].Visibility);
        Assert.Equal(Visibility.HiddenTab, PlacementMap(ws)[1].Visibility);
        Assert.True(ws.Perform(GlideAction.MoveWindowUp)); // Reorders tabs
        Assert.Equal([2, 1, 3], Ids(ws.Strip.Columns[0].Windows));
        Assert.Equal(0, ws.TabBars()[0].ActiveIndex);
    }

    [Fact]
    public void ConsumeAddsATabAndExpelMakesAPlainColumn()
    {
        var ws = Make();
        ws.Strip.Focus(4);
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelLeft));
        Assert.Single(ws.Strip.Columns);
        Assert.Equal([1, 2, 3, 4], Ids(ws.Strip.Columns[0].Windows));
        Assert.Equal(Visibility.Visible, PlacementMap(ws)[4].Visibility); // The consumed window becomes the active tab
        Assert.True(ws.Perform(GlideAction.ConsumeOrExpelRight));
        Assert.True(ws.Strip.Columns[0].IsTabbed);
        Assert.False(ws.Strip.Columns[1].IsTabbed);
    }

    [Fact]
    public void ParkedTabbedColumnHasNoTabBar()
    {
        Column[] columns = [Col([1, 2], isTabbed: true), .. Enumerable.Range(3, 3).Select(i => Col([i]))];
        var ws = new Workspace(new Strip(columns), viewport);
        ws.Perform(GlideAction.FocusLastColumn);
        Assert.Equal(Visibility.ParkedLeft, PlacementMap(ws)[1].Visibility);
        Assert.Empty(ws.TabBars());
    }

    [Fact]
    public void SnapshotKeepsTabs()
    {
        var strip = Make(focusedRow: 1).Strip;
        var decoded = RoundTrip(new StripSnapshot(strip, [], [], DateTimeOffset.UnixEpoch));
        var restored = decoded.Restore(W(1, 2, 3, 4), widthIndex: 1, presetCount: 4);
        Assert.True(restored.Columns[0].IsTabbed);
        Assert.Equal(1, restored.Columns[0].FocusedRow);
    }

    [Fact]
    public void SnapshotsFromBeforeTabsStillLoad()
    {
        const string json = """{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}],"userFloating":[],"userTiled":[],"bootTime":"1970-01-01T00:00:00Z"}""";
        var decoded = StripSnapshot.FromJson(json)!;
        Assert.False(decoded.Restore(W(1), widthIndex: 1, presetCount: 4).Columns[0].IsTabbed);
    }
}
