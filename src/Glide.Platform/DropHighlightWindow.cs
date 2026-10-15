using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>
/// Shows where a dragged window will land if let go now: an outline over the column it would
/// join, or a bar where it would go between another monitor's columns. Click-through, and stacked
/// just below the dragged window, so the drag itself is never in the way.
/// </summary>
internal sealed unsafe class DropHighlightWindow : IDisposable
{
    private const string ClassName = "GlideDropHighlight";
    /// <summary>How strongly an outlined column is tinted inside its border.</summary>
    private const double Tint = 0.15;
    private readonly WNDPROC procedure = (hwnd, message, wParam, lParam) => PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    private readonly HWND handle;
    private (Rect Frame, uint Color, int Border, int Radius)? shown;

    public DropHighlightWindow()
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
        // Layered + transparent: clicks go through, and it never takes focus.
        handle = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            ClassName, "Glide drop highlight", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, null, null);
        Native.ThrowIfFailed(handle != HWND.Null, "Creating the drop highlight");
    }

    /// <summary>
    /// Highlights <paramref name="frame"/> (screen pixels) just below <paramref name="dragged"/>.
    /// A slim frame is an insertion bar, filled solid; anything else gets a tinted outline.
    /// </summary>
    /// <param name="color">A COLORREF (0x00BBGGRR).</param>
    public void Show(HWND dragged, Rect frame, uint color, int border, int radius)
    {
        var x = (int)Math.Round(frame.X);
        var y = (int)Math.Round(frame.Y);
        var width = (int)Math.Round(frame.Width);
        var height = (int)Math.Round(frame.Height);
        if (width <= 0 || height <= 0) return;
        var isBar = Math.Min(width, height) < 12;
        if (isBar) radius = Math.Min(width, height) / 2;
        if (shown != (frame, color, border, radius)) Draw(x, y, width, height, color, isBar ? 0 : border, radius);
        // Inserted after the dragged window, which puts it directly beneath that window.
        PInvoke.SetWindowPos(handle, dragged, x, y, width, height,
                             SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        shown = (frame, color, border, radius);
    }

    public void Hide()
    {
        if (shown is null) return;
        PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_HIDE);
        shown = null;
    }

    /// <summary>Renders a rounded rectangle: a solid <paramref name="border"/> around a light tint, or all solid when there's no border.</summary>
    private void Draw(int x, int y, int width, int height, uint color, int border, int radius)
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
            var pixels = new Span<uint>(bits, width * height);
            byte red = (byte)(color & 0xFF), green = (byte)((color >> 8) & 0xFF), blue = (byte)((color >> 16) & 0xFF);
            // Premultiplied, as UpdateLayeredWindow wants.
            uint Pixel(double alpha)
            {
                var a = (uint)Math.Round(Math.Clamp(alpha, 0, 1) * 255);
                return (a << 24) | ((red * a / 255) << 16) | ((green * a / 255) << 8) | (blue * a / 255);
            }
            var fill = Pixel(border > 0 ? Tint : 1);
            // Away from the edges every pixel is the fill: only the band near them is worked out.
            var reach = Math.Max(radius, border) + 2;
            for (var row = 0; row < height; row++)
            {
                var line = pixels.Slice(row * width, width);
                var edgeRow = row < reach || row >= height - reach;
                for (var column = 0; column < width; column++)
                {
                    if (!edgeRow && column == reach && width - 2 * reach > 0)
                    {
                        line.Slice(reach, width - 2 * reach).Fill(fill);
                        column = width - reach - 1;
                        continue;
                    }
                    var px = column + 0.5;
                    var py = row + 0.5;
                    var outer = FocusRingWindow.Coverage(px, py, 0, 0, width, height, radius);
                    if (border <= 0)
                    {
                        line[column] = Pixel(outer);
                        continue;
                    }
                    var inner = FocusRingWindow.Coverage(px, py, border, border, width - border, height - border, Math.Max(radius - border, 0));
                    line[column] = Pixel(outer - inner + inner * Tint);
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

    public void Dispose() => PInvoke.DestroyWindow(handle);
}
