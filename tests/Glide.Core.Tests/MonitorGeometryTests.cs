using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class MonitorGeometryTests
{
    /// <summary>A laptop on the left, and a bigger monitor to its right, a bit higher.</summary>
    private static readonly Rect[] monitors = [new(0, 0, 1920, 1080), new(1920, -200, 2560, 1440)];

    [Fact]
    public void NeighboursGoByHorizontalPosition()
    {
        Assert.Equal(1, MonitorGeometry.Neighbour(monitors, 0, +1));
        Assert.Equal(0, MonitorGeometry.Neighbour(monitors, 1, -1));
        Assert.Null(MonitorGeometry.Neighbour(monitors, 0, -1));
        Assert.Null(MonitorGeometry.Neighbour(monitors, 1, +1));
        // Listed out of order (the primary first), still left to right.
        Rect[] reversed = [monitors[1], monitors[0]];
        Assert.Equal(0, MonitorGeometry.Neighbour(reversed, 1, +1));
    }

    [Fact]
    public void ColumnsParkedAcrossASharedEdgeWouldShowOnTheOtherMonitor()
    {
        // A column parked at the left monitor's right edge spills onto the right monitor.
        Assert.True(MonitorGeometry.ShowsOnAnother(new Rect(1916, 12, 588, 1056), 0, monitors));
        // Parked at its left edge, where there's no monitor, it only shows its sliver.
        Assert.False(MonitorGeometry.ShowsOnAnother(new Rect(-584, 12, 588, 1056), 0, monitors));
        Assert.False(MonitorGeometry.ShowsOnAnother(new Rect(12, 12, 588, 1056), 0, monitors));
    }

    [Fact]
    public void HiddenWindowsWaitAtTheDesktopsBottomRightCorner()
    {
        Assert.Equal(new Point(4479, 1239), MonitorGeometry.HiddenSpot(monitors));
        Assert.Equal(new Point(Viewport.MaxX - 1, Viewport.MaxY - 1), MonitorGeometry.HiddenSpot([Viewport]));
    }

    [Fact]
    public void DroppedWindowsLandBetweenTheColumnsAroundThePointer()
    {
        // Three ½ columns: centres at 306, 906 and 1506 on screen (12 + 294 + 600n).
        var ws = new Workspace(new Strip([Col([1]), Col([2]), Col([3])]), Viewport);
        Assert.Equal(0, MonitorGeometry.DropIndex(ws, 100));
        Assert.Equal(1, MonitorGeometry.DropIndex(ws, 500));
        Assert.Equal(2, MonitorGeometry.DropIndex(ws, 1100));
        Assert.Equal(3, MonitorGeometry.DropIndex(ws, 1700));
    }
}
