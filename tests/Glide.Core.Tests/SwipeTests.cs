using static Glide.Core.Tests.TestSupport;

namespace Glide.Core.Tests;

public class SwipeTrackerTests
{
    private static Touch[] Fingers(int count, double x, double y = 0.5) =>
        [.. Enumerable.Range(0, count).Select(i => new Touch(x + i * 0.05, y))];

    private static double ChangedDelta(SwipeTracker.Event? e) =>
        e is SwipeTracker.Event.Changed(var delta) ? delta : throw new Xunit.Sdk.XunitException($"Expected a change, got {e}");

    [Fact]
    public void HorizontalThreeFingerSwipe()
    {
        var tracker = new SwipeTracker();
        Assert.Empty(tracker.Update(Fingers(3, x: 0.5), 0));
        Assert.Empty(tracker.Update(Fingers(3, x: 0.505), 0.01)); // Below threshold
        var began = tracker.Update(Fingers(3, x: 0.48), 0.02);
        Assert.Equal(new SwipeTracker.Event.Began(), began[0]);
        Assert.Equal(2, began.Count);
        Assert.Equal(-0.02, ChangedDelta(began[1]), 1e-9);
        Assert.True(tracker.IsSwiping);

        var moved = tracker.Update(Fingers(3, x: 0.40), 0.06);
        Assert.Equal(-0.08, ChangedDelta(moved.FirstOrDefault()), 1e-9);

        var ended = tracker.Update([], 0.07);
        var velocity = Assert.IsType<SwipeTracker.Event.Ended>(ended.FirstOrDefault()).Velocity;
        Assert.Equal(-2.0, velocity, 1e-6); // 0.08 widths in 40 ms
        Assert.False(tracker.IsSwiping);
    }

    [Fact]
    public void FingersLandingOneAtATimeStillSwipe()
    {
        var tracker = new SwipeTracker();
        Assert.Empty(tracker.Update(Fingers(1, x: 0.5), 0));
        Assert.Empty(tracker.Update(Fingers(2, x: 0.5), 0.01));
        Assert.Empty(tracker.Update(Fingers(3, x: 0.5), 0.02));
        Assert.Equal(new SwipeTracker.Event.Began(), tracker.Update(Fingers(3, x: 0.45), 0.03).FirstOrDefault());
    }

    [Fact]
    public void FourFingersLockOutUntilLifted()
    {
        var tracker = new SwipeTracker();
        tracker.Update(Fingers(3, x: 0.5), 0);
        tracker.Update(Fingers(4, x: 0.5), 0.01);
        tracker.Update(Fingers(3, x: 0.5), 0.02);
        Assert.Empty(tracker.Update(Fingers(3, x: 0.3), 0.03)); // The system's four-finger swipe, not ours
        tracker.Update([], 0.04);
        tracker.Update(Fingers(3, x: 0.5), 0.05);
        Assert.Equal(new SwipeTracker.Event.Began(), tracker.Update(Fingers(3, x: 0.45), 0.06).FirstOrDefault());
    }

    [Fact]
    public void VerticalSwipeIsLeftToTheSystem()
    {
        var tracker = new SwipeTracker();
        tracker.Update(Fingers(3, x: 0.5, y: 0.5), 0);
        Assert.Empty(tracker.Update(Fingers(3, x: 0.5, y: 0.6), 0.02));
        Assert.Empty(tracker.Update(Fingers(3, x: 0.2, y: 0.6), 0.04)); // Locked until fingers lift
        tracker.Update([], 0.05);
        tracker.Update(Fingers(3, x: 0.5), 0.06);
        Assert.Equal(new SwipeTracker.Event.Began(), tracker.Update(Fingers(3, x: 0.45), 0.07).FirstOrDefault());
    }

    [Fact]
    public void OtherFingerCountsAreIgnored()
    {
        var tracker = new SwipeTracker();
        tracker.Update(Fingers(2, x: 0.5), 0);
        Assert.Empty(tracker.Update(Fingers(2, x: 0.2), 0.05));
        tracker.Update(Fingers(4, x: 0.5), 0.1);
        Assert.Empty(tracker.Update(Fingers(4, x: 0.2), 0.15));
    }

    [Fact]
    public void LiftingOneFingerEndsAndLocksUntilAllLift()
    {
        var tracker = new SwipeTracker();
        tracker.Update(Fingers(3, x: 0.5), 0);
        tracker.Update(Fingers(3, x: 0.45), 0.01);
        Assert.Single(tracker.Update(Fingers(2, x: 0.45), 0.02));
        tracker.Update(Fingers(3, x: 0.45), 0.03);
        Assert.Empty(tracker.Update(Fingers(3, x: 0.30), 0.04)); // Still locked
    }

    [Fact]
    public void PausedFingersReleaseWithoutVelocity()
    {
        var tracker = new SwipeTracker();
        tracker.Update(Fingers(3, x: 0.5), 0);
        tracker.Update(Fingers(3, x: 0.4), 0.01);
        tracker.Update(Fingers(3, x: 0.4), 0.30);
        Assert.Equal([new SwipeTracker.Event.Ended(0)], tracker.Update([], 0.31));
    }
}

public class GestureScrollTests
{
    /// <summary>Five ½ columns (588 wide, 600 apart); the usable width shows exactly two.</summary>
    private static Workspace Make(int focused = 0) => new(new Strip(Enumerable.Range(1, 5).Select(i => Col([i])), focused), Viewport);

    [Fact]
    public void ScrollFollowsFingersInsideBounds()
    {
        var ws = Make();
        ws.Scroll(250);
        Assert.Equal(250, ws.ScrollOffset);
        Assert.Equal((0, 1800), ws.ScrollBounds);
    }

    [Fact]
    public void RubberBandPastTheEnds()
    {
        var ws = Make();
        ws.Scroll(-300);
        Assert.Equal(-100, ws.ScrollOffset); // A third of the movement
        ws.Scroll(-3000);
        Assert.Equal(-297, ws.ScrollOffset); // At most a quarter of the screen
    }

    [Fact]
    public void SnapToNearestColumnEdge()
    {
        var ws = Make();
        Assert.Equal(0, ws.SnappedOffset(250));
        Assert.Equal(600, ws.SnappedOffset(350));
        Assert.Equal(1800, ws.SnappedOffset(5000));
        Assert.Equal(0, ws.SnappedOffset(-400));
    }

    [Fact]
    public void SettleKeepsFocusWhenStillVisible()
    {
        var ws = Make(focused: 1);
        ws.Scroll(500);
        Assert.False(ws.SettleSwipe(ws.ScrollOffset));
        Assert.Equal(600, ws.ScrollOffset);
        Assert.Equal(2, ws.Strip.FocusedWindow);
    }

    [Fact]
    public void SettleMovesFocusOntoScreen()
    {
        var ws = Make(focused: 0);
        ws.Scroll(1000);
        Assert.True(ws.SettleSwipe(1250));
        Assert.Equal(1200, ws.ScrollOffset);
        Assert.Equal(3, ws.Strip.FocusedWindow); // Columns 3 and 4 are on screen; 3 is nearest the middle when tied
        Assert.Equal(Visibility.Visible, ws.Placements().First(p => p.Window == 3).Visibility);
    }

    [Fact]
    public void SettleSpringsBackFromOverscroll()
    {
        var ws = Make(focused: 4);
        ws.EnsureFocusedVisible();
        Assert.Equal(1800, ws.ScrollOffset);
        ws.Scroll(600);
        Assert.True(ws.ScrollOffset > 1800);
        ws.SettleSwipe(ws.ScrollOffset);
        Assert.Equal(1800, ws.ScrollOffset);
    }

    [Fact]
    public void ShortStripStaysPut()
    {
        var ws = new Workspace(new Strip([Col([1])]), Viewport);
        ws.Scroll(400);
        ws.SettleSwipe(900);
        Assert.Equal(0, ws.ScrollOffset);
    }
}
