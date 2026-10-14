using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;

namespace Glide.Platform;

/// <summary>
/// A live preview of a window drawn by DWM into one of Glide's windows. Works for windows parked
/// off screen or waiting on another workspace, as long as they aren't minimized. Draws above the
/// host window's own content.
/// </summary>
public sealed unsafe class Thumbnail : IDisposable
{
    private nint handle;
    private readonly WindowId source;

    private Thumbnail(nint handle, WindowId source)
    {
        this.handle = handle;
        this.source = source;
    }

    /// <summary>Starts a preview of <paramref name="source"/> inside <paramref name="host"/>; null if DWM refuses.</summary>
    public static Thumbnail? Create(nint host, WindowId source)
    {
        nint id;
        var result = PInvoke.DwmRegisterThumbnail(new HWND((void*)host), Native.Hwnd(source), &id);
        return result.Succeeded ? new Thumbnail(id, source) : null;
    }

    /// <summary>
    /// Draws the preview in <paramref name="area"/> (pixels in the host's client area), as large as
    /// fits without stretching, centered. Only the visible window is shown, not its invisible
    /// resize borders.
    /// </summary>
    public Rect Show(Rect area, byte opacity = 255, Rect? clip = null)
    {
        if (handle == 0) return area;
        var hwnd = Native.Hwnd(source);
        var borders = Native.BordersOf(hwnd);
        PInvoke.GetWindowRect(hwnd, out var window);
        var width = Math.Max(window.right - window.left - borders.Left - borders.Right, 1);
        var height = Math.Max(window.bottom - window.top - borders.Top - borders.Bottom, 1);
        var scale = Math.Min(area.Width / width, area.Height / height);
        var shown = new Rect(Math.Round(area.X + (area.Width - width * scale) / 2), Math.Round(area.Y + (area.Height - height * scale) / 2),
                             Math.Round(width * scale), Math.Round(height * scale));
        // Cropped to the clip: the part of the source that falls inside it, scaled the same.
        var visible = clip is { } bounds ? shown.Intersection(bounds) : shown;
        if (visible is not { Width: > 0, Height: > 0 } part)
        {
            Hide();
            return shown;
        }
        var cropped = new RECT
        {
            left = borders.Left + (int)Math.Round((part.X - shown.X) / scale),
            top = borders.Top + (int)Math.Round((part.Y - shown.Y) / scale),
            right = borders.Left + (int)Math.Round((part.MaxX - shown.X) / scale),
            bottom = borders.Top + (int)Math.Round((part.MaxY - shown.Y) / scale),
        };
        var properties = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = PInvoke.DWM_TNP_RECTDESTINATION | PInvoke.DWM_TNP_RECTSOURCE | PInvoke.DWM_TNP_VISIBLE | PInvoke.DWM_TNP_OPACITY,
            rcDestination = new RECT { left = (int)part.X, top = (int)part.Y, right = (int)part.MaxX, bottom = (int)part.MaxY },
            rcSource = cropped,
            fVisible = true,
            opacity = opacity,
        };
        PInvoke.DwmUpdateThumbnailProperties(handle, &properties);
        return shown;
    }

    public void Hide()
    {
        if (handle == 0) return;
        var properties = new DWM_THUMBNAIL_PROPERTIES { dwFlags = PInvoke.DWM_TNP_VISIBLE, fVisible = false };
        PInvoke.DwmUpdateThumbnailProperties(handle, &properties);
    }

    public void Dispose()
    {
        if (handle == 0) return;
        PInvoke.DwmUnregisterThumbnail(handle);
        handle = 0;
    }
}
