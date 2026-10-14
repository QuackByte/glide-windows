using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Storage.FileSystem;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>An app icon as premultiplied BGRA pixels, ready for a WinUI bitmap.</summary>
public sealed record AppIcon(int Width, int Height, byte[] Pixels);

/// <summary>
/// Window icons for the overview and tab bars: the icon the window reports, else its class's,
/// else its executable's. Asking a hung app for its icon times out quickly instead of blocking.
/// </summary>
public static unsafe class AppIcons
{
    private const uint WM_GETICON = 0x007F;
    private static readonly Dictionary<WindowId, AppIcon?> cache = [];

    public static AppIcon? Of(WindowId id)
    {
        if (cache.TryGetValue(id, out var known)) return known;
        if (cache.Count > 256) cache.Clear();
        var icon = Load(Native.Hwnd(id));
        cache[id] = icon;
        return icon;
    }

    private static AppIcon? Load(HWND hwnd)
    {
        // ICON_SMALL2, ICON_SMALL, ICON_BIG.
        foreach (var kind in new nuint[] { 2, 0, 1 })
        {
            nuint result;
            if (PInvoke.SendMessageTimeout(hwnd, WM_GETICON, kind, 0, SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG, 100, &result).Value != 0
                && result != 0 && Pixels(new HICON((void*)result)) is { } icon)
                return icon;
        }
        foreach (var index in new[] { GET_CLASS_LONG_INDEX.GCLP_HICONSM, GET_CLASS_LONG_INDEX.GCL_HICON })
        {
            var handle = PInvoke.GetClassLongPtr(hwnd, index);
            if (handle != 0 && Pixels(new HICON((void*)handle)) is { } icon) return icon;
        }
        // Store apps and others that don't say: the executable's icon.
        if (Native.ExecutablePath(Native.ProcessId(hwnd)) is { } path)
        {
            var info = new SHFILEINFOW();
            if (PInvoke.SHGetFileInfo(path, 0, ref info, SHGFI_FLAGS.SHGFI_ICON | SHGFI_FLAGS.SHGFI_SMALLICON) != 0 && !info.hIcon.IsNull)
            {
                try
                {
                    return Pixels(info.hIcon);
                }
                finally
                {
                    PInvoke.DestroyIcon(info.hIcon);
                }
            }
        }
        return null;
    }

    /// <summary>Reads an icon's pixels; icons without an alpha channel get one from their mask.</summary>
    private static AppIcon? Pixels(HICON icon)
    {
        ICONINFO info;
        if (!PInvoke.GetIconInfo(icon, &info)) return null;
        try
        {
            if (info.hbmColor.IsNull) return null;
            BITMAP bitmap;
            if (PInvoke.GetObject(info.hbmColor, sizeof(BITMAP), &bitmap) == 0) return null;
            int width = bitmap.bmWidth, height = bitmap.bmHeight;
            if (width <= 0 || height <= 0 || width > 256 || height > 256) return null;

            var color = Read(info.hbmColor, width, height);
            if (color is null) return null;
            var hasAlpha = false;
            for (var i = 3; i < color.Length; i += 4) hasAlpha |= color[i] != 0;
            if (!hasAlpha && !info.hbmMask.IsNull && Read(info.hbmMask, width, height) is { } mask)
            {
                // A black mask pixel is part of the icon; white is transparent.
                for (var i = 0; i < color.Length; i += 4) color[i + 3] = mask[i] == 0 ? (byte)255 : (byte)0;
            }
            for (var i = 0; i < color.Length; i += 4)
            {
                var alpha = color[i + 3];
                color[i] = (byte)(color[i] * alpha / 255);
                color[i + 1] = (byte)(color[i + 1] * alpha / 255);
                color[i + 2] = (byte)(color[i + 2] * alpha / 255);
            }
            return new AppIcon(width, height, color);
        }
        finally
        {
            if (!info.hbmColor.IsNull) PInvoke.DeleteObject(info.hbmColor);
            if (!info.hbmMask.IsNull) PInvoke.DeleteObject(info.hbmMask);
        }
    }

    private static byte[]? Read(HBITMAP bitmap, int width, int height)
    {
        var info = new BITMAPINFO();
        info.bmiHeader = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER), biWidth = width, biHeight = -height, biPlanes = 1, biBitCount = 32,
        };
        var pixels = new byte[width * height * 4];
        var screen = PInvoke.GetDC(HWND.Null);
        try
        {
            fixed (byte* bytes = pixels)
                return PInvoke.GetDIBits(screen, bitmap, 0, (uint)height, bytes, &info, DIB_USAGE.DIB_RGB_COLORS) == height ? pixels : null;
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, screen);
        }
    }
}
