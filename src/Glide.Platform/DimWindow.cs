using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>
/// Dims unfocused windows: a click-through black window covering the focused window's monitor,
/// stacked just below the focused window, so everything behind it goes darker and it stays bright.
/// </summary>
internal sealed unsafe class DimWindow : IDisposable
{
    private const string ClassName = "GlideDim";
    private readonly WNDPROC procedure = (hwnd, message, wParam, lParam) => PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    private readonly HWND handle;
    private byte? opacity;
    private bool visible;

    public DimWindow()
    {
        fixed (char* name = ClassName)
        {
#pragma warning disable CS8500 // The delegate field marshals as a function pointer of the same size.
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = procedure,
                hInstance = (HINSTANCE)(nint)PInvoke.GetModuleHandle((string?)null).DangerousGetHandle(),
                // Painted black; the window's opacity does the dimming.
                hbrBackground = new HBRUSH(PInvoke.GetStockObject(GET_STOCK_OBJECT_FLAGS.BLACK_BRUSH).Value),
                lpszClassName = name,
            };
#pragma warning restore CS8500
            PInvoke.RegisterClassEx(windowClass);
        }
        // Layered + transparent: clicks go through, and it never takes focus.
        handle = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_LAYERED | WINDOW_EX_STYLE.WS_EX_TRANSPARENT | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE,
            ClassName, "Glide dimming", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0, HWND.Null, null, null, null);
        Native.ThrowIfFailed(handle != HWND.Null, "Creating the dimming");
    }

    /// <summary>Covers <paramref name="monitor"/> with dimming, just below <paramref name="window"/>.</summary>
    /// <param name="amount">How dark: 0 is none, 1 is black.</param>
    public void Show(HWND window, Rect monitor, double amount)
    {
        var level = (byte)Math.Round(Math.Clamp(amount, 0, 1) * 255);
        if (opacity != level)
        {
            PInvoke.SetLayeredWindowAttributes(handle, new COLORREF(0), level, LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA);
            opacity = level;
        }
        // Inserted after the focused window, which puts it directly beneath that window.
        PInvoke.SetWindowPos(handle, window, (int)Math.Round(monitor.X), (int)Math.Round(monitor.Y),
                             (int)Math.Round(monitor.Width), (int)Math.Round(monitor.Height),
                             SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
        visible = true;
    }

    public void Hide()
    {
        if (!visible) return;
        PInvoke.ShowWindow(handle, SHOW_WINDOW_CMD.SW_HIDE);
        visible = false;
    }

    public void Dispose() => PInvoke.DestroyWindow(handle);
}
