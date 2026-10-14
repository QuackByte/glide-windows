using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>Window styling for Glide's own overlays: tab bars, the on-screen display and the overview.</summary>
public static unsafe class OverlayWindows
{
    /// <summary>
    /// Keeps a window off the taskbar and Alt+Tab, optionally never activated by clicks (so a tab
    /// bar doesn't take focus from the app it belongs to), with Windows 11's rounded corners.
    /// </summary>
    public static void Configure(nint window, bool activates)
    {
        var hwnd = new HWND((void*)window);
        var style = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE) | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW;
        if (!activates) style |= WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        PInvoke.SetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (int)(uint)style);
        // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2.
        var round = 2;
        PInvoke.DwmSetWindowAttribute(hwnd, (DWMWINDOWATTRIBUTE)33, &round, sizeof(int));
    }

    /// <summary>Moves a window to <paramref name="frame"/> (screen pixels) just above <paramref name="window"/> in z-order, without activating it.</summary>
    public static void PlaceAbove(nint overlay, Rect frame, WindowId window)
    {
        var hwnd = new HWND((void*)overlay);
        var above = PInvoke.GetWindow(Native.Hwnd(window), GET_WINDOW_CMD.GW_HWNDPREV);
        // Already right above it, or the window is at the top: either way, go to the top.
        var insertAfter = above == HWND.Null || above == hwnd ? HWND.Null /* HWND_TOP */ : above;
        PInvoke.SetWindowPos(hwnd, insertAfter, (int)frame.X, (int)frame.Y, (int)frame.Width, (int)frame.Height,
                             SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }

    /// <summary>The primary display's work area in pixels, and its scale.</summary>
    public static (Rect WorkArea, double Scale) PrimaryDisplay => Native.PrimaryDisplay();
}
