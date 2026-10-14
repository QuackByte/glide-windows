namespace Glide.Core.Tests;

public class FrameAnimationTests
{
    private static readonly Rect viewport = new(0, 40, 1200, 800);

    [Fact]
    public void EaseOutEndpointsAndShape()
    {
        Assert.Equal(0, FrameAnimation.EaseOut(0));
        Assert.Equal(1, FrameAnimation.EaseOut(1));
        Assert.Equal(1, FrameAnimation.EaseOut(2)); // Clamped past the end
        Assert.True(FrameAnimation.EaseOut(0.5) > 0.5, "Ease-out covers most distance early");
    }

    [Fact]
    public void FrameInterpolatesOriginAndUsesTargetSize()
    {
        var from = new Rect(0, 52, 600, 776);
        var to = new Rect(1000, 52, 400, 776);
        Assert.Equal(new Rect(0, 52, 400, 776), FrameAnimation.Frame(from, to, progress: 0));
        Assert.Equal(to, FrameAnimation.Frame(from, to, progress: 1));
        Assert.Equal(875, FrameAnimation.Frame(from, to, progress: 0.5).MinX);
    }

    [Fact]
    public void ParkedToParkedJumps()
    {
        var parkedLeft = new Rect(-596, 52, 600, 776);
        var parkedRight = new Rect(1196, 52, 600, 776);
        Assert.False(FrameAnimation.ShouldAnimate(parkedLeft, parkedRight, viewport));
    }

    [Fact]
    public void VisibleMovesAnimate()
    {
        var onScreen = new Rect(12, 52, 600, 776);
        var parked = new Rect(1196, 52, 600, 776);
        Assert.True(FrameAnimation.ShouldAnimate(onScreen, parked, viewport));
        Assert.True(FrameAnimation.ShouldAnimate(parked, onScreen, viewport));
    }

    [Fact]
    public void TinyMovesJump()
    {
        var a = new Rect(12, 52, 600, 776);
        Assert.False(FrameAnimation.ShouldAnimate(a, a.Offset(1, 0), viewport));
        Assert.False(FrameAnimation.ShouldAnimate(a, new Rect(12, 52, 400, 776), viewport)); // Resize in place has nothing to travel
    }
}
