namespace Glide.Core.Tests;

public class RestorePlannerTests
{
    private static readonly Rect viewport = new(0, 40, 1200, 800);

    [Fact]
    public void VisibleWindowsKeepTheirFrames()
    {
        var frame = new Rect(100, 100, 400, 300);
        var result = RestorePlanner.RestoredFrames([(1, frame)], viewport);
        Assert.Equal(frame, result[1]);
    }

    [Fact]
    public void MostlyVisibleWindowIsNudgedInside()
    {
        var frame = new Rect(1000, 100, 400, 300);
        var result = RestorePlanner.RestoredFrames([(1, frame)], viewport);
        Assert.Equal(new Rect(1200 - 12 - 400, 100, 400, 300), result[1]);
    }

    [Fact]
    public void ParkedFullHeightWindowsShrinkAndStagger()
    {
        (WindowId, Rect)[] parked = [(1, new Rect(-596, 52, 600, 776)), (2, new Rect(1196, 52, 600, 776))];
        var result = RestorePlanner.RestoredFrames(parked, viewport);
        Assert.Equal(new Rect(12, 52, 600, 660), result[1]);
        Assert.Equal(new Rect(40, 80, 600, 660), result[2]);
        Assert.All(result.Values, frame => Assert.True(viewport.Contains(frame)));
    }

    [Fact]
    public void SmallParkedWindowsCascadeDiagonally()
    {
        var parked = Enumerable.Range(1, 3).Select(i => ((WindowId)i, new Rect(1196, 52, 400, 300)));
        var result = RestorePlanner.RestoredFrames(parked, viewport);
        Assert.Equal(new Point(12, 52), result[1].Origin);
        Assert.Equal(new Point(40, 80), result[2].Origin);
        Assert.Equal(new Point(68, 108), result[3].Origin);
    }

    [Fact]
    public void OversizedWindowShrinksToFit()
    {
        var frame = new Rect(-2000, 0, 3000, 2000);
        var result = RestorePlanner.RestoredFrames([(1, frame)], viewport);
        Assert.Equal(new Rect(12, 52, 1000, 660), result[1]);
    }
}
