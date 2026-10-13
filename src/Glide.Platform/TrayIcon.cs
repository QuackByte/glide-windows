using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>The notification-area icon: the active workspace's number, and Glide's menu.</summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const uint CallbackMessage = Win32.WM_APP + 1;
    private const uint IconId = 1;
    private const nuint ToggleCommand = 1, SettingsCommand = 2, QuitCommand = 4;

    private readonly MessageWindow window;
    private readonly uint taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private HICON icon;
    private int workspace = 1;
    private bool enabled = true;
    private bool added;

    public TrayIcon(MessageWindow window)
    {
        this.window = window;
        Add();
    }

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action? QuitRequested;

    /// <summary>The toggle's shortcut, shown in the menu.</summary>
    public string ToggleShortcut { get; set; } = "";

    public void Update(int workspaceNumber, bool tilingEnabled)
    {
        if (workspaceNumber == workspace && tilingEnabled == enabled && added) return;
        workspace = workspaceNumber;
        enabled = tilingEnabled;
        Modify();
    }

    /// <summary>Returns true if the message was the tray's.</summary>
    public bool Handle(uint message, WPARAM wParam, LPARAM lParam)
    {
        if (message == taskbarCreated)
        {
            // Explorer restarted: the icon is gone.
            added = false;
            Add();
            return true;
        }
        if (message == Win32.WM_SETTINGCHANGE)
        {
            // The taskbar may have switched between light and dark.
            Modify();
            return false;
        }
        if (message == Win32.WM_COMMAND)
        {
            switch (wParam.Value & 0xFFFF)
            {
                case ToggleCommand: ToggleRequested?.Invoke(); break;
                case SettingsCommand: SettingsRequested?.Invoke(); break;
                case QuitCommand: QuitRequested?.Invoke(); break;
                default: return false;
            }
            return true;
        }
        if (message != CallbackMessage) return false;
        var notification = (uint)(lParam.Value & 0xFFFF);
        if (notification is Win32.WM_CONTEXTMENU or Win32.NIN_SELECT or Win32.NIN_KEYSELECT or Win32.WM_LBUTTONUP)
            ShowMenu();
        return true;
    }

    private void ShowMenu()
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            var shortcut = ToggleShortcut.Length > 0 ? $"\t{ToggleShortcut}" : "";
            Append(menu, MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_GRAYED, 0, $"Glide · Workspace {workspace}");
            Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            Append(menu, MENU_ITEM_FLAGS.MF_STRING | (enabled ? MENU_ITEM_FLAGS.MF_CHECKED : 0), ToggleCommand, $"Tiling{shortcut}");
            Append(menu, MENU_ITEM_FLAGS.MF_STRING, SettingsCommand, "Settings…");
            Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            Append(menu, MENU_ITEM_FLAGS.MF_STRING, QuitCommand, "Quit Glide");

            PInvoke.GetCursorPos(out var point);
            // The menu only closes on an outside click if its owner is in the foreground.
            PInvoke.SetForegroundWindow(window.Handle);
            PInvoke.TrackPopupMenuEx(menu, (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN), point.X, point.Y, window.Handle, null);
            PInvoke.PostMessage(window.Handle, 0, default, default);
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
        }
    }

    private static void Append(HMENU menu, MENU_ITEM_FLAGS flags, nuint id, string? text)
    {
        fixed (char* chars = text)
            PInvoke.AppendMenu(menu, flags, id, chars);
    }

    private NOTIFYICONDATAW Data()
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = window.Handle,
            uID = IconId,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP,
            uCallbackMessage = CallbackMessage,
            hIcon = icon,
        };
        data.szTip = $"Glide · Workspace {workspace}{(enabled ? "" : " · Tiling off")}";
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        return data;
    }

    private void RenderIcon()
    {
        var old = icon;
        icon = IconRenderer.Render(workspace, enabled, LightTaskbar());
        if (!old.IsNull) PInvoke.DestroyIcon(old);
    }

    private void Add()
    {
        RenderIcon();
        var data = Data();
        added = PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, data);
        if (added) PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, data);
    }

    private void Modify()
    {
        if (!added)
        {
            Add();
            return;
        }
        RenderIcon();
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, Data());
    }

    private static bool LightTaskbar()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }

    public void Dispose()
    {
        if (added) PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, Data());
        if (!icon.IsNull) PInvoke.DestroyIcon(icon);
    }
}

/// <summary>Draws the tray icon: a rounded square with the workspace number knocked out of it.</summary>
internal static unsafe class IconRenderer
{
    public static HICON Render(int number, bool enabled, bool lightTaskbar)
    {
        var dpi = PInvoke.GetDpiForSystem();
        var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
        var text = DigitCoverage(number.ToString(), size);

        var info = new BITMAPINFO();
        info.bmiHeader = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER),
            biWidth = size,
            biHeight = -size,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };
        void* bits;
        var color = PInvoke.CreateDIBSection(HDC.Null, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
        var pixels = (uint*)bits;
        // White on a dark taskbar, near-black on a light one; dimmed while tiling is off.
        var rgb = lightTaskbar ? 0x1C1C1Cu : 0xFFFFFFu;
        var opacity = enabled ? 1.0 : 0.45;
        var radius = size / 4.5;
        var inset = Math.Max(1, size / 16);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var shape = RoundedSquareCoverage(x + 0.5, y + 0.5, inset, size - inset, radius);
                var alpha = shape * (1 - text[y * size + x]) * opacity;
                var a = (uint)Math.Round(alpha * 255);
                // Icons take straight (not premultiplied) BGRA.
                pixels[y * size + x] = (a << 24) | rgb;
            }
        }
        var mask = PInvoke.CreateBitmap(size, size, 1, 1, null);
        var iconInfo = new ICONINFO { fIcon = true, hbmColor = color, hbmMask = mask };
        var icon = PInvoke.CreateIconIndirect(&iconInfo);
        PInvoke.DeleteObject(color);
        PInvoke.DeleteObject(mask);
        return icon;
    }

    private static double RoundedSquareCoverage(double x, double y, double min, double max, double radius)
    {
        var cx = Math.Clamp(x, min + radius, max - radius);
        var cy = Math.Clamp(y, min + radius, max - radius);
        var distance = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        var edge = Math.Min(Math.Min(x - min, max - x), Math.Min(y - min, max - y));
        return distance > 0 ? Math.Clamp(radius - distance + 0.5, 0, 1) : Math.Clamp(edge + 0.5, 0, 1);
    }

    /// <summary>How much of each pixel the text covers, 0…1, rendered by GDI in grayscale.</summary>
    private static double[] DigitCoverage(string text, int size)
    {
        var info = new BITMAPINFO();
        info.bmiHeader = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = size, biHeight = -size, biPlanes = 1, biBitCount = 32,
        };
        var dc = PInvoke.CreateCompatibleDC(HDC.Null);
        void* bits;
        var bitmap = PInvoke.CreateDIBSection(dc, &info, DIB_USAGE.DIB_RGB_COLORS, &bits, HANDLE.Null, 0);
        var oldBitmap = PInvoke.SelectObject(dc, bitmap);
        // 700 = FW_BOLD, 1 = DEFAULT_CHARSET, 4 = OUT_TT_PRECIS, 4 = ANTIALIASED_QUALITY (grayscale, no ClearType fringes).
        var font = new HGDIOBJ(CreateFontW(-(int)Math.Round(size * 0.78), 0, 0, 0, 700, 0, 0, 0, 1, 4, 0, 4, 0, "Segoe UI"));
        var oldFont = PInvoke.SelectObject(dc, font);
        PInvoke.SetTextColor(dc, new COLORREF(0x00FFFFFF));
        PInvoke.SetBkMode(dc, BACKGROUND_MODE.TRANSPARENT);
        var rect = new RECT { left = 0, top = -Math.Max(1, size / 32), right = size, bottom = size };
        fixed (char* chars = text)
            PInvoke.DrawText(dc, chars, text.Length, ref rect, DRAW_TEXT_FORMAT.DT_CENTER | DRAW_TEXT_FORMAT.DT_VCENTER | DRAW_TEXT_FORMAT.DT_SINGLELINE);
        PInvoke.GdiFlush();

        var coverage = new double[size * size];
        var pixels = (uint*)bits;
        for (var i = 0; i < coverage.Length; i++)
            coverage[i] = ((pixels[i] >> 8) & 0xFF) / 255.0;

        PInvoke.SelectObject(dc, oldFont);
        PInvoke.SelectObject(dc, oldBitmap);
        PInvoke.DeleteObject(font);
        PInvoke.DeleteObject(bitmap);
        PInvoke.DeleteDC(dc);
        return coverage;
    }

    // CsWin32's CreateFont wrapper marshals its byte-sized enums wrongly, so this one is declared by hand.
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern nint CreateFontW(int height, int width, int escapement, int orientation, int weight, uint italic, uint underline,
        uint strikeOut, uint charSet, uint outPrecision, uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);
}
