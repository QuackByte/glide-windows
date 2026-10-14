using System.Runtime.InteropServices;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>Message and event numbers Glide uses, from WinUser.h.</summary>
internal static class Win32
{
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_DISPLAYCHANGE = 0x007E;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_TIMER = 0x0113;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_APP = 0x8000;
    public const uint NIN_SELECT = 0x0400;
    public const uint NIN_KEYSELECT = 0x0401;

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
    public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
    public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    public const uint EVENT_OBJECT_DESTROY = 0x8001;
    public const uint EVENT_OBJECT_SHOW = 0x8002;
    public const uint EVENT_OBJECT_HIDE = 0x8003;
    public const uint EVENT_OBJECT_CLOAKED = 0x8017;
    public const uint EVENT_OBJECT_UNCLOAKED = 0x8018;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    public const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    /// <summary>An unassigned key. Pressing it while Win is down stops the Start menu from opening, like AutoHotkey's MenuMaskKey.</summary>
    public const ushort VK_NONAME = 0xFF;
}

/// <summary>Invisible resize borders around a window: the difference between its window rect and what DWM draws.</summary>
internal readonly record struct Borders(int Left, int Top, int Right, int Bottom);

/// <summary>What Glide needs to know about a window to decide whether to tile it.</summary>
/// <param name="IsCandidate">A top-level app window, the kind Alt+Tab shows. Others are never touched.</param>
/// <param name="CanTile">Glide could tile it: resizable, not a dialog, and not elevated beyond Glide's reach.</param>
/// <param name="ClassifiedTileable">Glide tiles it by default: a standard window with a maximize button.</param>
internal sealed record WindowFacts(bool IsCandidate, bool CanTile, bool ClassifiedTileable, string ClassName, string Title, string? AppId);

internal static unsafe class Native
{
    private static readonly uint ownProcessId = (uint)Environment.ProcessId;
    private static readonly Lazy<bool> selfElevated = new(() => IsElevated(PInvoke.GetCurrentProcess(), ownsHandle: false) ?? false);

    /// <summary>Shell surfaces and system popups that look like app windows but must never be moved.</summary>
    private static readonly HashSet<string> ignoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "MultitaskingViewFrame", "ForegroundStaging", "NotifyIconOverflowWindow",
        "TopLevelWindowForOverflowXamlIsland", "Windows.Internal.Shell.TabProxyWindow", "SysShadow", "tooltips_class32",
    };

    public static WindowId Id(HWND hwnd) => new((long)(nint)hwnd.Value);

    public static HWND Hwnd(WindowId id) => new((void*)(nint)id.Value);

    public static bool IsSelfElevated => selfElevated.Value;

    // Inspection

    public static string ClassName(HWND hwnd)
    {
        Span<char> buffer = stackalloc char[256];
        var length = PInvoke.GetClassName(hwnd, buffer);
        return length > 0 ? new string(buffer[..length]) : "";
    }

    public static string Title(HWND hwnd)
    {
        var length = PInvoke.GetWindowTextLength(hwnd);
        if (length <= 0) return "";
        var buffer = new char[length + 1];
        var copied = PInvoke.GetWindowText(hwnd, buffer);
        return new string(buffer, 0, Math.Max(copied, 0));
    }

    public static WINDOW_STYLE Style(HWND hwnd) => (WINDOW_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);

    public static WINDOW_EX_STYLE ExStyle(HWND hwnd) => (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);

    public static bool IsCloaked(HWND hwnd)
    {
        int cloaked = 0;
        return PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(int)).Succeeded && cloaked != 0;
    }

    public static uint ProcessId(HWND hwnd)
    {
        PInvoke.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    public static WindowFacts Inspect(HWND hwnd)
    {
        var className = ClassName(hwnd);
        var title = Title(hwnd);
        var style = Style(hwnd);
        var exStyle = ExStyle(hwnd);
        var pid = ProcessId(hwnd);
        var isAppWindow = exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_APPWINDOW);

        var candidate = PInvoke.IsWindowVisible(hwnd)
            && PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT) == hwnd
            && !style.HasFlag(WINDOW_STYLE.WS_CHILD)
            && pid != ownProcessId
            && !ignoredClasses.Contains(className)
            && title.Length > 0
            && (!exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) || isAppWindow)
            && !exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE)
            // Owned windows are dialogs, palettes and the like: they float with their owner.
            && (PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_OWNER) == HWND.Null || isAppWindow)
            && !IsCloaked(hwnd)
            && !PInvoke.IsIconic(hwnd);
        if (!candidate) return new WindowFacts(false, false, false, className, title, null);

        var resizable = style.HasFlag(WINDOW_STYLE.WS_THICKFRAME);
        var hasCaption = (style & WINDOW_STYLE.WS_CAPTION) == WINDOW_STYLE.WS_CAPTION;
        var dialog = className == "#32770";
        // Without UIAccess or elevation, Windows won't let Glide move windows of elevated apps (UIPI).
        var unreachable = !IsSelfElevated && (IsProcessElevated(pid) ?? true);
        var canTile = resizable && !dialog && !unreachable;
        var tileable = canTile && hasCaption && style.HasFlag(WINDOW_STYLE.WS_MAXIMIZEBOX);
        return new WindowFacts(true, canTile, tileable, className, title, AppId(hwnd, pid, className));
    }

    /// <summary>
    /// The executable name of a window's app. Store apps run inside ApplicationFrameHost, so for
    /// those it's the process behind the hosted CoreWindow.
    /// </summary>
    private static string? AppId(HWND hwnd, uint pid, string className)
    {
        if (className == "ApplicationFrameWindow")
        {
            uint hosted = 0;
            PInvoke.EnumChildWindows(hwnd, (child, _) =>
            {
                var childPid = ProcessId(child);
                if (childPid == pid) return true;
                hosted = childPid;
                return false;
            }, default);
            if (hosted != 0) pid = hosted;
        }
        return ExecutableName(pid);
    }

    public static string? ExecutableName(uint pid)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process.IsNull) return null;
        try
        {
            var buffer = new char[1024];
            uint size = (uint)buffer.Length;
            fixed (char* chars = buffer)
            {
                if (!PInvoke.QueryFullProcessImageName(process, PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32, chars, &size)) return null;
            }
            return Path.GetFileName(new string(buffer, 0, (int)size));
        }
        finally
        {
            PInvoke.CloseHandle(process);
        }
    }

    /// <summary>Whether a process runs elevated; null if Windows won't say, which usually means it does.</summary>
    private static bool? IsProcessElevated(uint pid)
    {
        var process = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        return process.IsNull ? null : IsElevated(process, ownsHandle: true);
    }

    private static bool? IsElevated(HANDLE process, bool ownsHandle)
    {
        try
        {
            HANDLE token;
            if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token)) return null;
            try
            {
                TOKEN_ELEVATION elevation;
                uint length;
                if (!PInvoke.GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenElevation, &elevation, (uint)sizeof(TOKEN_ELEVATION), &length)) return null;
                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                PInvoke.CloseHandle(token);
            }
        }
        finally
        {
            if (ownsHandle) PInvoke.CloseHandle(process);
        }
    }

    /// <summary>Top-level windows in z-order, front to back.</summary>
    public static List<HWND> TopLevelWindows()
    {
        var result = new List<HWND>();
        PInvoke.EnumWindows((hwnd, _) =>
        {
            result.Add(hwnd);
            return true;
        }, default);
        return result;
    }

    // Geometry

    private static Rect ToRect(RECT r) => new(r.left, r.top, r.right - r.left, r.bottom - r.top);

    /// <summary>The frame as the user sees it, without the invisible resize borders.</summary>
    public static Rect? VisibleFrame(HWND hwnd)
    {
        RECT bounds;
        if (PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &bounds, (uint)sizeof(RECT)).Succeeded)
            return ToRect(bounds);
        return PInvoke.GetWindowRect(hwnd, out var rect) ? ToRect(rect) : null;
    }

    public static Borders BordersOf(HWND hwnd)
    {
        RECT bounds;
        if (!PInvoke.GetWindowRect(hwnd, out var rect)
            || PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &bounds, (uint)sizeof(RECT)).Failed)
            return default;
        var borders = new Borders(bounds.left - rect.left, bounds.top - rect.top, rect.right - bounds.right, rect.bottom - bounds.bottom);
        // Guard against nonsense from windows in transition (minimized, cloaked).
        return borders is { Left: >= 0 and < 64, Top: >= 0 and < 64, Right: >= 0 and < 64, Bottom: >= 0 and < 64 } ? borders : default;
    }

    /// <summary>
    /// Places a window so its visible frame lands on <paramref name="frame"/>. Asynchronous, so a
    /// hung app can't stall Glide.
    /// </summary>
    public static void SetVisibleFrame(HWND hwnd, Rect frame, Borders borders)
    {
        var flags = SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER
                    | SET_WINDOW_POS_FLAGS.SWP_ASYNCWINDOWPOS;
        PInvoke.SetWindowPos(hwnd, HWND.Null,
            (int)Math.Round(frame.X) - borders.Left, (int)Math.Round(frame.Y) - borders.Top,
            (int)Math.Round(frame.Width) + borders.Left + borders.Right, (int)Math.Round(frame.Height) + borders.Top + borders.Bottom,
            flags);
    }

    /// <summary>
    /// Takes a window out of the maximized or snapped state without activating it. Windows keeps a
    /// snapped window at its snap size, so it has to be un-snapped before Glide can size it.
    /// </summary>
    public static void Unmaximize(HWND hwnd)
    {
        if (PInvoke.IsZoomed(hwnd) || IsSnapped(hwnd)) PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    /// <summary>Snapped by Windows (Snap layouts, Win+arrows, or dragging to a screen edge).</summary>
    public static bool IsSnapped(HWND hwnd)
    {
        try
        {
            return PInvoke.IsWindowArranged(hwnd);
        }
        catch (EntryPointNotFoundException)
        {
            // Before Windows 10 2004.
            return false;
        }
    }

    /// <summary>The primary display's work area (without the taskbar) and its scale factor.</summary>
    public static (Rect WorkArea, double Scale) PrimaryDisplay()
    {
        var monitor = PInvoke.MonitorFromPoint(new System.Drawing.Point(0, 0), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        PInvoke.GetMonitorInfo(monitor, ref info);
        var scale = PInvoke.GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpi, out _).Succeeded ? dpi / 96.0 : 1.0;
        return (ToRect(info.rcWork), scale);
    }

    // Focus ring

    /// <summary>Colors a window's border (Windows 11; ignored on Windows 10). Null restores the default.</summary>
    public static void SetBorderColor(HWND hwnd, uint? colorref)
    {
        // DWMWA_BORDER_COLOR = 34; DWMWA_COLOR_DEFAULT = 0xFFFFFFFF.
        var value = colorref ?? 0xFFFFFFFF;
        PInvoke.DwmSetWindowAttribute(hwnd, (DWMWINDOWATTRIBUTE)34, &value, sizeof(uint));
    }

    /// <summary>A COLORREF (0x00BBGGRR) from "accent" or "#RRGGBB"; the accent color if unreadable.</summary>
    public static uint ParseColor(string color)
    {
        if (color.StartsWith('#') && color.Length == 7 && uint.TryParse(color[1..], System.Globalization.NumberStyles.HexNumber, null, out var rgb))
            return ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        // AccentColor is 0xAABBGGRR.
        return key?.GetValue("AccentColor") is int accent ? (uint)accent & 0x00FFFFFF : 0x00D47800;
    }

    // Focus

    /// <summary>
    /// Brings a window to the front. Windows only lets the process behind the last input event do
    /// that, so Glide first injects an empty mouse event (the PowerToys workaround).
    /// </summary>
    public static bool Focus(HWND hwnd)
    {
        if (PInvoke.GetForegroundWindow() == hwnd) return true;
        if (PInvoke.IsIconic(hwnd)) PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        Span<INPUT> input = [new INPUT { type = INPUT_TYPE.INPUT_MOUSE }];
        PInvoke.SendInput(input, sizeof(INPUT));
        if (PInvoke.SetForegroundWindow(hwnd)) return true;
        PInvoke.BringWindowToTop(hwnd);
        return PInvoke.SetForegroundWindow(hwnd);
    }

    /// <summary>Gives focus to the desktop, e.g. after switching to an empty workspace.</summary>
    public static void FocusDesktop()
    {
        var shell = PInvoke.GetShellWindow();
        if (shell != HWND.Null) Focus(shell);
    }

    public static HWND Foreground => PInvoke.GetForegroundWindow();

    public static bool Exists(HWND hwnd) => PInvoke.IsWindow(hwnd);

    public static void ThrowIfFailed(bool ok, string what)
    {
        if (!ok) throw new InvalidOperationException($"{what} failed: {Marshal.GetLastPInvokeErrorMessage()}");
    }
}
