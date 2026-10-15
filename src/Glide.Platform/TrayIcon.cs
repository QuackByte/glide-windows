using System.Runtime.InteropServices;
using Glide.Core;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using static Glide.Core.Strings;

namespace Glide.Platform;

/// <summary>The notification-area icon: the active workspace's number, and Glide's menu.</summary>
internal sealed unsafe class TrayIcon : IDisposable
{
    private const uint CallbackMessage = Win32.WM_APP + 1;
    private const uint IconId = 1;
    private const nuint ToggleCommand = 1, SettingsCommand = 2, StartAtSignInCommand = 3, QuitCommand = 4, UpdateCommand = 5, WhatsNewCommand = 6,
        ShortcutSheetCommand = 7, ReportProblemCommand = 8;
    /// <summary>Menu items that run an action have this plus the action's number as their command.</summary>
    private const nuint ActionCommandBase = 0x100;
    private const uint NIN_BALLOONUSERCLICK = 0x0405;
    private string? update;

    private readonly MessageWindow window;
    private readonly uint taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
    private HICON icon;
    private int workspace = 1;
    private IReadOnlyList<int> inUse = [];
    private bool enabled = true;
    private bool added;

    public TrayIcon(MessageWindow window)
    {
        this.window = window;
        Add();
    }

    public event Action? ToggleRequested;
    public event Action? SettingsRequested;
    public event Action? UpdateRequested;
    public event Action? WhatsNewRequested;
    public event Action? ShortcutSheetRequested;
    public event Action? ReportProblemRequested;
    /// <summary>An action picked from the menu: a workspace, or one from Shortcuts.</summary>
    public event Action<GlideAction>? ActionRequested;

    /// <summary>What the menu shows besides its fixed items: asked for each time it opens.</summary>
    public sealed record MenuContents(
        string? Summary,
        IReadOnlyList<(int Number, int Windows, bool IsActive, string? Shortcut)> Workspaces,
        IReadOnlyList<(string Group, IReadOnlyList<(GlideAction Action, string Title, string? Shortcut)> Actions)> Shortcuts,
        string? ShortcutSheetShortcut,
        IReadOnlyList<string> GivenBack);

    public Func<MenuContents>? Contents { get; set; }

    /// <summary>Offers a newer version in the menu, and says so once in a notification.</summary>
    public void OfferUpdate(string version)
    {
        if (update == version) return;
        update = version;
        var data = Data();
        data.uFlags |= NOTIFY_ICON_DATA_FLAGS.NIF_INFO;
        data.szInfoTitle = T("Glide {0} is available", version);
        data.szInfo = T("Click to install it. Glide puts your windows back, updates, and starts again.");
        data.dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO;
        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, data);
    }
    public event Action? QuitRequested;

    /// <summary>The toggle's shortcut, shown in the menu.</summary>
    public string ToggleShortcut { get; set; } = "";

    /// <param name="workspacesInUse">The workspaces with windows on them, listed in the tooltip and the menu; the icon shows only the active one.</param>
    public void Update(int workspaceNumber, bool tilingEnabled, IReadOnlyList<int> workspacesInUse)
    {
        if (workspaceNumber == workspace && tilingEnabled == enabled && workspacesInUse.SequenceEqual(inUse) && added) return;
        workspace = workspaceNumber;
        enabled = tilingEnabled;
        inUse = workspacesInUse;
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
                case StartAtSignInCommand: StartupEntry.IsEnabled = !StartupEntry.IsEnabled; break;
                case UpdateCommand: UpdateRequested?.Invoke(); break;
                case WhatsNewCommand: WhatsNewRequested?.Invoke(); break;
                case QuitCommand: QuitRequested?.Invoke(); break;
                case ShortcutSheetCommand: ShortcutSheetRequested?.Invoke(); break;
                case ReportProblemCommand: ReportProblemRequested?.Invoke(); break;
                case var command when command >= ActionCommandBase && Enum.IsDefined((GlideAction)(int)(command - ActionCommandBase)):
                    ActionRequested?.Invoke((GlideAction)(int)(command - ActionCommandBase));
                    break;
                default: return false;
            }
            return true;
        }
        if (message != CallbackMessage) return false;
        var notification = (uint)(lParam.Value & 0xFFFF);
        Log.Debug($"Tray icon notification 0x{notification:X}");
        if (notification == NIN_BALLOONUSERCLICK && update is not null)
        {
            UpdateRequested?.Invoke();
            return true;
        }
        if (notification is Win32.WM_CONTEXTMENU or Win32.NIN_SELECT or Win32.NIN_KEYSELECT or Win32.WM_LBUTTONUP)
            ShowMenu();
        return true;
    }

    private void ShowMenu()
    {
        var menu = PInvoke.CreatePopupMenu();
        try
        {
            const MENU_ITEM_FLAGS text = MENU_ITEM_FLAGS.MF_STRING, label = MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_GRAYED;
            static string Keys(string? shortcut) => string.IsNullOrEmpty(shortcut) ? "" : $"\t{shortcut}";
            MenuContents? contents = null;
            try
            {
                contents = Contents?.Invoke();
            }
            catch (Exception e)
            {
                // The fixed items still work.
                Log.Error("Couldn't list the tray menu's workspaces and shortcuts", e);
            }

            Append(menu, label, 0, T("Glide · Workspace {0}", workspace));
            if (enabled && contents?.Summary is { } summary) Append(menu, label, 0, summary);
            if (enabled && contents is { Workspaces.Count: > 1 })
            {
                Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
                foreach (var (number, windows, isActive, shortcut) in contents.Workspaces)
                {
                    var count = windows == 1 ? T("1 window") : T("{0} windows", windows);
                    Append(menu, text | (isActive ? MENU_ITEM_FLAGS.MF_CHECKED : 0), ActionCommand(GlideAction.FocusWorkspace1 + number - 1),
                           T("Workspace {0} — {1}", number, count) + Keys(shortcut));
                }
            }
            Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            Append(menu, text | (enabled ? MENU_ITEM_FLAGS.MF_CHECKED : 0), ToggleCommand, T("Tiling") + Keys(ToggleShortcut));
            Append(menu, text | (StartupEntry.IsEnabled ? MENU_ITEM_FLAGS.MF_CHECKED : 0), StartAtSignInCommand, T("Start Glide When I Sign In"));
            if (contents is not null) AppendShortcuts(menu, contents, Keys);
            Append(menu, text, SettingsCommand, T("Settings…"));
            if (contents is { GivenBack.Count: > 0 } given)
            {
                // Like a clash on the Mac: these keys do Windows' thing, not Glide's. Settings can take them back.
                var count = given.GivenBack.Count;
                var warning = PInvoke.CreatePopupMenu();
                Append(warning, text, SettingsCommand, T("Open Settings to Take Them Back…"));
                Append(warning, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
                foreach (var line in given.GivenBack) Append(warning, label, 0, line);
                Append(menu, text | MENU_ITEM_FLAGS.MF_POPUP, (nuint)(nint)warning.Value,
                       count == 1 ? T("⚠ 1 Shortcut Left to Windows") : T("⚠ {0} Shortcuts Left to Windows", count));
            }
            if (update is not null) Append(menu, text, UpdateCommand, T("Install Glide {0}", update));
            Append(menu, text, WhatsNewCommand, T("What's New"));
            Append(menu, text, ReportProblemCommand, T("Report a Problem…"));
            Append(menu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
            Append(menu, text, QuitCommand, T("Quit Glide"));

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

    /// <summary>Shortcuts ▸ every action by group, each runnable from here, and the sheet that lists them all.</summary>
    private static void AppendShortcuts(HMENU menu, MenuContents contents, Func<string?, string> keys)
    {
        var shortcuts = PInvoke.CreatePopupMenu();
        Append(shortcuts, MENU_ITEM_FLAGS.MF_STRING, ShortcutSheetCommand, T("Show All Shortcuts") + keys(contents.ShortcutSheetShortcut));
        Append(shortcuts, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        foreach (var (group, actions) in contents.Shortcuts)
        {
            var submenu = PInvoke.CreatePopupMenu();
            foreach (var (action, title, shortcut) in actions)
                Append(submenu, MENU_ITEM_FLAGS.MF_STRING, ActionCommand(action), $"{title}{keys(shortcut)}");
            Append(shortcuts, MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_POPUP, (nuint)(nint)submenu.Value, group);
        }
        Append(menu, MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_POPUP, (nuint)(nint)shortcuts.Value, T("Shortcuts"));
    }

    private static nuint ActionCommand(GlideAction action) => ActionCommandBase + (nuint)(int)action;

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
        var others = inUse.Count > 1 ? " · " + T("In use: {0}", string.Join(", ", inUse)) : "";
        data.szTip = T("Glide · Workspace {0}", workspace) + others + (enabled ? "" : " · " + T("Tiling off"));
        data.Anonymous.uVersion = PInvoke.NOTIFYICON_VERSION_4;
        return data;
    }

    private void RenderIcon()
    {
        var old = icon;
        icon = IconRenderer.Render(workspace, inUse, enabled, LightTaskbar());
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

/// <summary>
/// Draws the tray icon: a rounded square with the workspace number knocked out of it. With several
/// workspaces in use, a 3×3 grid of the nine workspaces instead, the ones in use lit and the active one brightest.
/// </summary>
internal static unsafe class IconRenderer
{
    public static HICON Render(int number, IReadOnlyList<int> inUse, bool enabled, bool lightTaskbar)
    {
        var dpi = PInvoke.GetDpiForSystem();
        var size = PInvoke.GetSystemMetricsForDpi(SYSTEM_METRICS_INDEX.SM_CXSMICON, dpi);
        var text = DigitCoverage(number.ToString(), size);
        var map = inUse.Count > 1 ? MiniMap(size, number, inUse) : null;

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
                var alpha = map is not null
                    ? map[y * size + x] * opacity
                    : RoundedSquareCoverage(x + 0.5, y + 0.5, inset, size - inset, radius) * (1 - text[y * size + x]) * opacity;
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

    /// <summary>Nine squares in a 3×3 grid, numbered in reading order: the active one fully lit, the others in use half, the rest faint.</summary>
    private static double[] MiniMap(int size, int active, IReadOnlyList<int> inUse)
    {
        var map = new double[size * size];
        var cell = Math.Max(1, size / 4);
        var gap = Math.Max(1, size / 16);
        var grid = 3 * cell + 2 * gap;
        var origin = (size - grid) / 2;
        for (var number = 1; number <= 9; number++)
        {
            var left = origin + (number - 1) % 3 * (cell + gap);
            var top = origin + (number - 1) / 3 * (cell + gap);
            var level = number == active ? 1.0 : inUse.Contains(number) ? 0.5 : 0.18;
            for (var y = top; y < top + cell; y++)
                for (var x = left; x < left + cell; x++)
                    map[y * size + x] = level;
        }
        return map;
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
