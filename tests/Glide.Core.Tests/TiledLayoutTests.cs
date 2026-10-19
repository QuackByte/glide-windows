using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class TiledLayoutTests
{
    private static Workspace Make(int columns, int[]? widths = null, LayoutMode? layout = LayoutMode.Tiled, int focused = 0) =>
        new(new Strip(Enumerable.Range(1, columns).Select(i => Col([i], widthIndex: widths?[i - 1] ?? 1)), focused), Viewport) { LayoutOverride = layout };

    private static double Total(Workspace ws) =>
        Enumerable.Range(0, ws.Strip.Columns.Count).Sum(ws.Width) + ws.Settings.Gap * (ws.Strip.Columns.Count - 1);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void ColumnsFillTheViewportExactly(int count)
    {
        var ws = Make(count);
        Assert.Equal(ws.UsableWidth, Total(ws));
        var frames = Frames(ws);
        Assert.Equal(12, frames[1].MinX);
        Assert.Equal(Viewport.MaxX - 12, frames[count].MaxX);
        Assert.All(ws.Placements(), p => Assert.Equal(Visibility.Visible, p.Visibility));
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Fact]
    public void DefaultWidthsGiveEqualColumns()
    {
        var ws = Make(3);
        Assert.Equal(ws.Width(0), ws.Width(1), 1);
        Assert.Equal(ws.Width(1), ws.Width(2), 1);
    }

    [Fact]
    public void PresetsActAsWeights()
    {
        // ⅓ and ⅔ share the room 1 : 2.
        var ws = Make(2, widths: [0, 2]);
        Assert.Equal(1176, ws.Width(0) + ws.Width(1));
        Assert.Equal(388, ws.Width(0));
        Assert.Equal(788, ws.Width(1));
    }

    [Fact]
    public void CustomWidthsActAsWeights()
    {
        var ws = Make(2);
        ws.Strip.SetCustomWidth(300, 1);
        ws.Strip.SetCustomWidth(900, 2);
        Assert.Equal(294, ws.Width(0));
        Assert.Equal(882, ws.Width(1));
    }

    [Fact]
    public void MinimumWidthsAreKeptAndOthersShareTheRest()
    {
        var ws = Make(3);
        ws.MinimumWidths[1] = 600;
        Assert.Equal(600, ws.Width(0));
        Assert.Equal(ws.UsableWidth, Total(ws));
        Assert.Equal(ws.Width(1), ws.Width(2), 1);
    }

    [Fact]
    public void OverflowingMinimumsScrollLikeTheStrip()
    {
        var ws = Make(2);
        ws.MinimumWidths[1] = 800;
        ws.MinimumWidths[2] = 800;
        Assert.Equal(800, ws.Width(0));
        Assert.Equal(800, ws.Width(1));
        Assert.True(ws.ContentWidth > ws.UsableWidth);
        ws.Strip.Focus(2);
        ws.EnsureFocusedVisible();
        Assert.True(ws.ScrollOffset > 0);
    }

    [Fact]
    public void FullWidthZoomsOneColumnAndTheRestStayTiled()
    {
        var ws = Make(3, focused: 1);
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(ws.UsableWidth, ws.Width(1));
        // The others split the room between themselves, as if the zoomed column weren't there.
        Assert.Equal(588, ws.Width(0));
        Assert.Equal(588, ws.Width(2));
        var visibility = Visibilities(ws);
        Assert.Equal(Visibility.Visible, visibility[2]);
        Assert.NotEqual(Visibility.Visible, visibility[1]);
        ws.Perform(GlideAction.ToggleFullWidth);
        Assert.Equal(0, ws.ScrollOffset);
        Assert.Equal(ws.UsableWidth, Total(ws));
    }

    [Fact]
    public void CyclingWidthChangesTheShare()
    {
        var ws = Make(2);
        var before = ws.Width(0);
        ws.Perform(GlideAction.CycleColumnWidth);
        Assert.True(ws.Width(0) > before);
        Assert.Equal(ws.UsableWidth, Total(ws));
    }

    [Fact]
    public void CenteringAndSwipesDoNothingWhenEverythingFits()
    {
        var ws = Make(3, focused: 1);
        Assert.False(ws.Perform(GlideAction.CenterColumn));
        Assert.Equal((0, 0), ws.ScrollBounds);
        ws.SettleSwipe(500);
        Assert.Equal(0, ws.ScrollOffset);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(700)]
    [InlineData(900)]
    public void ResizeLandsNearTheRequestedWidth(double wanted)
    {
        var ws = Make(3);
        var others = (ws.Width(1), ws.Width(2));
        Assert.True(ws.SetColumnWidthFromResize(1, wanted));
        Assert.InRange(ws.Width(0), wanted - 2, wanted + 2);
        Assert.Equal(ws.UsableWidth, Total(ws));
        // The neighbours keep their proportions.
        Assert.Equal(others.Item1 / (double)others.Item2, ws.Width(1) / (double)ws.Width(2), 2);
    }

    [Fact]
    public void ResizeIsClampedToSomethingSensible()
    {
        var ws = Make(2);
        ws.SetColumnWidthFromResize(1, 5000);
        Assert.InRange(ws.Width(1), 50, 300);
        ws.SetColumnWidthFromResize(1, 1);
        Assert.InRange(ws.Width(0), 50, 200);
    }

    [Fact]
    public void ResizeOnTheStripIsTheCustomWidth()
    {
        var ws = Make(2, layout: LayoutMode.Scrolling);
        ws.SetColumnWidthFromResize(1, 700);
        Assert.Equal(700, ws.Width(0));
        Assert.Equal(588, ws.Width(1));
    }

    [Fact]
    public void ScrollingWidthsAreUnchanged()
    {
        var ws = Make(3, layout: null);
        Assert.Equal(LayoutMode.Scrolling, ws.Layout);
        Assert.Equal(588, ws.Width(0));
    }

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
    public void TogglingToTiledBringsTheStripBackToZero()
    {
        var ws = Make(4, layout: null, focused: 3);
        ws.EnsureFocusedVisible();
        Assert.True(ws.ScrollOffset > 0);
        ws.ToggleLayout();
        Assert.Equal(LayoutMode.Tiled, ws.Layout);
        Assert.Equal(0, ws.ScrollOffset);
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
    public void MovingAColumnLeavesBothLayoutsAlone()
    {
        var set = new WorkspaceSet(Viewport);
        set.Insert(1, 1);
        set.Active.ToggleLayout();
        set.MoveFocusedWindow(1);
        Assert.Equal(LayoutMode.Tiled, set.Workspaces[0].LayoutOverride);
        Assert.Null(set.Workspaces[1].LayoutOverride);
    }

    [Fact]
    public void SnapshotKeepsLayoutOverrides()
    {
        var set = new WorkspaceSet(Viewport);
        set.Insert(1, 1);
        set.Workspaces[0].ToggleLayout();
        set.Workspaces[2].LayoutOverride = LayoutMode.Scrolling;
        var json = new StripSnapshot(set, [], [], DateTimeOffset.UnixEpoch).ToJson();
        Assert.Contains("\"Tiled\"", json);
        var decoded = StripSnapshot.FromJson(json)!;
        Assert.Equal(LayoutMode.Tiled, decoded.Layouts[0]);
        Assert.Null(decoded.Layouts[1]);
        Assert.Equal(LayoutMode.Scrolling, decoded.Layouts[2]);

        var restored = new WorkspaceSet(Viewport);
        var (strips, active) = decoded.RestoreWorkspaces(W(1), 1, 4);
        restored.Restore(strips, active, decoded.Layouts);
        Assert.Equal(LayoutMode.Tiled, restored.Workspaces[0].LayoutOverride);
        Assert.Null(restored.Workspaces[1].LayoutOverride);
        Assert.Equal(LayoutMode.Scrolling, restored.Workspaces[2].LayoutOverride);
    }

    [Fact]
    public void SnapshotsWithoutLayoutsStillLoad()
    {
        const string json = """{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}],"workspaces":[{"columns":[{"windows":[1],"focusedRow":0,"widthIndex":1,"isFullWidth":false}]}],"userFloating":[],"userTiled":[],"bootTime":"1970-01-01T00:00:00Z"}""";
        var decoded = StripSnapshot.FromJson(json)!;
        Assert.Equal<LayoutMode?[]>([null], [.. decoded.Layouts]);
    }

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
}
