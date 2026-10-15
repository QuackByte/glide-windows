using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class DropHighlightTests
{
    [Fact]
    public void AColumnsFrameCoversItsWholeStack()
    {
        var ws = new Workspace(new Strip([Col([1, 2]), Col([3])]), Viewport);
        Assert.Equal(new Rect(12, 52, 588, 776), ws.ColumnFrame(new WindowId(2)));
        Assert.Equal(new Rect(612, 52, 588, 776), ws.ColumnFrame(new WindowId(3)));
        Assert.Null(ws.ColumnFrame(new WindowId(9)));
    }

    [Fact]
    public void ATabbedColumnsFrameIncludesItsTabBar()
    {
        var ws = new Workspace(new Strip([Col([1, 2, 3], isTabbed: true), Col([4])]), Viewport);
        // The 28 px bar at the top plus the active tab below it.
        Assert.Equal(new Rect(12, 52, 588, 776), ws.ColumnFrame(new WindowId(3)));
    }

    [Fact]
    public void ColumnsOffScreenHaveNoFrameAndPartlyShownOnesAreClipped()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2]), Col([3])]), Viewport);
        // The third ½ column starts at the screen's right edge: parked.
        Assert.Null(ws.ColumnFrame(new WindowId(3)));

        // ⅔ columns: the second starts at 812 and runs past the right edge.
        var wide = new Workspace(new Strip([Col([1], widthIndex: 2), Col([2], widthIndex: 2)]), Viewport);
        Assert.Equal(new Rect(812, 52, 400, 776), wide.ColumnFrame(new WindowId(2)));
    }

    [Fact]
    public void InsertionBarsSitInTheGapBetweenColumns()
    {
        var ws = new Workspace(new Strip([Col([1]), Col([2])]), Viewport);
        Assert.Equal(new Rect(4, 52, 4, 776), ws.InsertionFrame(0, 4));
        Assert.Equal(new Rect(604, 52, 4, 776), ws.InsertionFrame(1, 4));
        Assert.Equal(new Rect(1204, 52, 4, 776), ws.InsertionFrame(2, 4));
        // Past the screen's edge, the bar stays at the edge.
        var three = new Workspace(new Strip([Col([1]), Col([2]), Col([3])]), Viewport);
        Assert.Equal(new Rect(1208, 52, 4, 776), three.InsertionFrame(3, 4));
        // An empty strip: at the left edge.
        Assert.Equal(new Rect(4, 52, 4, 776), new Workspace(Viewport).InsertionFrame(0, 4));
    }
}
