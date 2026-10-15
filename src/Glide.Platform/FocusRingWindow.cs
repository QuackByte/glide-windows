using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>
/// The focus ring: a click-through window of Glide's drawn just outside the focused window and
/// stacked right above it. Windows' own border color (DWMWA_BORDER_COLOR) is a hairline that
/// apps drawing their own frame, like Electron apps, don't show at all; this works for every
/// window and can be any thickness.
/// </summary>
internal sealed unsafe class FocusRingWindow : IDisposable
{
    private const string ClassName = "GlideFocusRing";
    private readonly WNDPROC procedure = (hwnd, message, wParam, lParam) => PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    private readonly HWND handle;
    /// <summary>What's drawn now, so moving the ring doesn't redraw it.</summary>
    private (int Width, int Height, uint Color, int Thickness, int Radius)? drawn;
    private bool visible;

    public FocusRingWindow()
    {
        fixed (char* name = ClassName)
        {
#pragma warning disable CS8500 // The delegate field marshals as a function pointer of the same size.
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = procedure,
                hInstance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((string?)null).DangerousGetHandle(),
                lpszClassName = name,
            };
#pragma warning restore CS8500
            PInvoke.RegisterClassEx(windowClass);
        }
        // Layered + transparent: clicks go through to the window below.
        handle = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            ClassName, "Glide focus ring", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, null, null);
        Native.ThrowIfFailed(handle != HWND.Null, "Creating the focus ring");
    }

    /// <summary>
    /// Draws the ring around <paramref name="frame"/> (the window's visible frame, in pixels) and
    /// puts it just above <paramref name="window"/>.
    /// </summary>
    /// <param name="color">A COLORREF (0x00BBGGRR).</param>
    /// <param name="radius">The window's corner radius, so the ring follows it.</param>
    public void Show(HWND window, Rect frame, uint color, int thickness, int radius)
    {
        var x = (int)Math.Round(frame.X) - thickness;
        var y = (int)Math.Round(frame.Y) - thickness;
        var width = (int)Math.Round(frame.Width) + 2 * thickness;
        var height = (int)Math.Round(frame.Height) + 2 * thickness;
        if (width <= 0 || height <= 0) return;
        if (drawn != (width, height, color, thickness, radius))
        {
            Draw(x, y, width, height, color, thickness, radius);
            drawn = (width, height, color, thickness, radius);
        }
        var above = PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_HWNDPREV);
        var insertAfter = above == HWND.Null || above == handle ? HWND.Null /* HWND_TOP */ : above;
        PInvoke.SetWindowPos(handle, insertAfter, x, y, width, height,
                             SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        visible = true;
    }

    public void Hide()
    {
        if (!visible) return;
        PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_HIDE);
        visible = false;
    }

    /// <summary>Renders the band between the window's rounded edge and one <paramref name="thickness"/> further out, antialiased.</summary>
    private void Draw(int x, int y, int width, int height, uint color, int thickness, int radius)
    {
        var info = new BITMAPINFO();
        info.bmiHeader = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32,
        };
        var screen = PInvoke.GetDC(HWND.Null);
        var dc = PInvoke.CreateCompatibleDC(screen);
        void* bits;
        var bitmap = PInvoke.CreateDIBSection(dc, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
        var old = PInvoke.SelectObject(dc, bitmap);
        try
        {
            var pixels = (uint*)bits;
            byte red = (byte)(color & 0xFF), green = (byte)((color >> 8) & 0xFF), blue = (byte)((color >> 16) & 0xFF);
            var outerRadius = radius + thickness;
            // Only the band near the edge has anything in it; the DIB starts out transparent.
            var reach = outerRadius + 2;
            for (var row = 0; row < height; row++)
            {
                var edgeRow = row < reach || row >= height - reach;
                for (var column = 0; column < width; column++)
                {
                    if (!edgeRow && column >= reach && column < width - reach) column = width - reach;
                    var px = column + 0.5;
                    var py = row + 0.5;
                    var outer = Coverage(px, py, 0, 0, width, height, outerRadius);
                    var inner = Coverage(px, py, thickness, thickness, width - thickness, height - thickness, radius);
                    var alpha = Math.Clamp(outer - inner, 0, 1);
                    if (alpha <= 0) continue;
                    var a = (uint)Math.Round(alpha * 255);
                    // Premultiplied, as UpdateLayeredWindow wants.
                    pixels[row * width + column] = (a << 24) | ((red * a / 255) << 16) | ((green * a / 255) << 8) | (blue * a / 255);
                }
            }
            var size = new SIZE { cx = width, cy = height };
            var source = new System.Drawing.Point(0, 0);
            var destination = new System.Drawing.Point(x, y);
            var blend = new BLENDFUNCTION { BlendOp = 0 /* AC_SRC_OVER */, SourceConstantAlpha = 255, AlphaFormat = 1 /* AC_SRC_ALPHA */ };
            PInvoke.UpdateLayeredWindow(handle, screen, &destination, &size, dc, &source, new COLORREF(0), &blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA);
        }
        finally
        {
            PInvoke.SelectObject(dc, old);
            PInvoke.DeleteObject(bitmap);
            PInvoke.DeleteDC(dc);
            PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }

    /// <summary>How much of the pixel at (x, y) lies inside a rounded rectangle, 0…1.</summary>
    internal static double Coverage(double x, double y, double left, double top, double right, double bottom, double radius)
    {
        // Signed distance to a rounded rectangle, negative inside.
        var halfWidth = (right - left) / 2;
        var halfHeight = (bottom - top) / 2;
        var r = Math.Min(radius, Math.Min(halfWidth, halfHeight));
        var qx = Math.Abs(x - (left + halfWidth)) - (halfWidth - r);
        var qy = Math.Abs(y - (top + halfHeight)) - (halfHeight - r);
        var outside = Math.Sqrt(Math.Max(qx, 0) * Math.Max(qx, 0) + Math.Max(qy, 0) * Math.Max(qy, 0));
        var distance = outside + Math.Min(Math.Max(qx, qy), 0) - r;
        return Math.Clamp(0.5 - distance, 0, 1);
    }

    public void Dispose() => PInvoke.DestroyWindow(handle);
}
