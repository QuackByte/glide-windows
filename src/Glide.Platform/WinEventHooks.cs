using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Glide.Platform;

/// <summary>
/// Window lifecycle events from every process: shown, hidden, destroyed, focused, minimized,
/// cloaked, moved or resized by the user, and moved at all (to notice windows going full screen). Delivered on the thread that installs the hooks.
/// </summary>
internal sealed unsafe class WinEventHooks : IDisposable
{
    private static readonly (uint Min, uint Max)[] ranges =
    [
        (Win32.EVENT_SYSTEM_FOREGROUND, Win32.EVENT_SYSTEM_FOREGROUND),
        (Win32.EVENT_SYSTEM_MOVESIZESTART, Win32.EVENT_SYSTEM_MOVESIZEEND),
        (Win32.EVENT_SYSTEM_MINIMIZESTART, Win32.EVENT_SYSTEM_MINIMIZEEND),
        (Win32.EVENT_OBJECT_DESTROY, Win32.EVENT_OBJECT_HIDE),
        (Win32.EVENT_OBJECT_LOCATIONCHANGE, Win32.EVENT_OBJECT_LOCATIONCHANGE),
        (Win32.EVENT_OBJECT_CLOAKED, Win32.EVENT_OBJECT_UNCLOAKED),
    ];

    private readonly WINEVENTPROC callback;
    private readonly List<UnhookWinEventSafeHandle> hooks = [];

    public WinEventHooks(Action<uint, HWND> handler)
    {
        callback = (_, @event, hwnd, idObject, idChild, _, _) =>
        {
            // Only whole windows, not their scroll bars, carets or child objects.
            if (idObject != 0 || idChild != 0 || hwnd == HWND.Null) return;
            try
            {
                handler(@event, hwnd);
            }
            catch (Exception e)
            {
                Log.Error($"Handling event 0x{@event:X} for 0x{(nint)hwnd.Value:X} failed", e);
            }
        };
        foreach (var (min, max) in ranges)
        {
            var hook = PInvoke.SetWinEventHook(min, max, default, callback, 0, 0, Win32.WINEVENT_OUTOFCONTEXT | Win32.WINEVENT_SKIPOWNPROCESS);
            Native.ThrowIfFailed(!hook.IsInvalid, "Installing window event hooks");
            hooks.Add(hook);
        }
    }

    public void Dispose()
    {
        foreach (var hook in hooks) hook.Dispose();
        hooks.Clear();
    }
}
