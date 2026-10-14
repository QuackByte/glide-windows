using Glide.Platform;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Glide;

/// <summary>Turns a WinUI window into one of Glide's overlays: no title bar or border, not in Alt+Tab.</summary>
internal static class Overlay
{
    public static nint Prepare(Window window, bool topmost, bool activates)
    {
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = topmost;
        }
        window.AppWindow.IsShownInSwitchers = false;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        OverlayWindows.Configure(hwnd, activates);
        return hwnd;
    }

    /// <summary>Pixels per XAML unit on the primary display.</summary>
    public static double Scale => OverlayWindows.PrimaryDisplay.Scale;
}
