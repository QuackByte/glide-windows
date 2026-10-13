namespace Glide.Core;

/// <summary>Chooses on-screen frames for windows when tiling stops, so nothing is left parked off screen.</summary>
public static class RestorePlanner
{
    /// <summary>
    /// Windows already fully inside the viewport keep their frame. Mostly visible windows are
    /// nudged inside. The rest (parked columns) are cascaded from the top-left corner at up to
    /// <paramref name="cascadeScale"/> of the viewport, so a stack of full-height windows still staggers.
    /// </summary>
    public static Dictionary<WindowId, Rect> RestoredFrames(IEnumerable<(WindowId Window, Rect Frame)> frames, Rect viewport,
                                                            double inset = 12, double cascadeStep = 28, double cascadeScale = 0.85)
    {
        var bounds = viewport.Inset(inset, inset);
        var result = new Dictionary<WindowId, Rect>();
        var cascadeIndex = 0;

        foreach (var (window, frame) in frames)
        {
            if (viewport.Contains(frame))
            {
                result[window] = frame;
                continue;
            }
            var overlap = frame.Intersection(viewport);
            var mostlyVisible = overlap is { } o && o.Width >= frame.Width / 2 && o.Height >= frame.Height / 2;
            var width = Math.Min(frame.Width, bounds.Width);
            var height = Math.Min(frame.Height, bounds.Height);
            double x, y;
            if (mostlyVisible)
            {
                (x, y) = (frame.X, frame.Y);
            }
            else
            {
                width = Math.Min(width, Rounding.Round(bounds.Width * cascadeScale));
                height = Math.Min(height, Rounding.Round(bounds.Height * cascadeScale));
                var steps = (int)(Math.Min(bounds.Width - width, bounds.Height - height) / cascadeStep);
                var step = cascadeIndex % (steps + 1);
                (x, y) = (bounds.MinX + step * cascadeStep, bounds.MinY + step * cascadeStep);
                cascadeIndex++;
            }
            x = Math.Min(Math.Max(x, bounds.MinX), bounds.MaxX - width);
            y = Math.Min(Math.Max(y, bounds.MinY), bounds.MaxY - height);
            result[window] = new Rect(x, y, width, height);
        }
        return result;
    }
}
