namespace Glide.Core;

/// <summary>Timing and interpolation for moving windows between layouts.</summary>
public static class FrameAnimation
{
    /// <summary>Short enough to feel instant, long enough to show where a column went.</summary>
    public const double DefaultDuration = 0.12;

    /// <summary>Cubic ease-out: fast start, gentle landing.</summary>
    public static double EaseOut(double t)
    {
        var clamped = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - clamped, 3);
    }

    /// <summary>
    /// Size jumps to the target immediately; only the origin travels. Resizing every frame makes
    /// apps re-layout their content and stutter.
    /// </summary>
    public static Rect Frame(Rect from, Rect to, double progress)
    {
        var p = EaseOut(progress);
        return new Rect(
            Rounding.Round(from.MinX + (to.MinX - from.MinX) * p),
            Rounding.Round(from.MinY + (to.MinY - from.MinY) * p),
            to.Width,
            to.Height);
    }

    /// <summary>
    /// Animate only moves the user can see: a window going from one parked spot to another (both
    /// off screen) just jumps, as do moves of a pixel or two.
    /// </summary>
    public static bool ShouldAnimate(Rect from, Rect to, Rect viewport, double minimumVisible = 8)
    {
        if (Math.Abs(from.MinX - to.MinX) <= 1 && Math.Abs(from.MinY - to.MinY) <= 1) return false;
        bool Visible(Rect rect) => rect.Intersection(viewport) is { } overlap && overlap.Width >= minimumVisible;
        return Visible(from) || Visible(to);
    }
}
