using System.Collections.Frozen;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>
/// Glide's shortcuts, through a low-level keyboard hook. RegisterHotKey refuses the Win combos
/// Windows reserves (Win+arrows and others), so the hook sees every key first and swallows
/// Glide's combos before Windows does, the way FancyZones' "Override Windows Snap" works.
/// </summary>
/// <remarks>
/// Windows silently removes a low-level hook whose callback takes longer than
/// LowLevelHooksTimeout, so the hook runs on its own thread and the callback only matches and
/// swallows; <paramref name="onAction"/> must hand the action to the UI thread and return.
/// </remarks>
internal sealed unsafe class KeyboardHook(Action<GlideAction> onAction, Action<double> onScroll) : IDisposable
{
    private volatile FrozenDictionary<KeyCombo, GlideAction> bindings = FrozenDictionary<KeyCombo, GlideAction>.Empty;
    private readonly HashSet<uint> swallowedKeys = [];
    /// <summary>Tags input Glide sends itself.</summary>
    private const nuint InjectedMarker = 0x476C6964; // "Glid"

    private HOOKPROC? callback, mouseCallback;
    private HHOOK hook, mouseHook;

    /// <summary>Scroll wheels move the strip only while tiling is on.</summary>
    public volatile bool ScrollEnabled;
    /// <summary>When the horizontal wheel (tilt, or a thumb wheel like the MX Master's) scrolls the strip.</summary>
    public volatile WheelMode HorizontalWheel = WheelMode.Always;
    private Thread? thread;
    private uint threadId;
    /// <summary>A combo with Win fired since Win went down, so releasing Win must not open Start.</summary>
    private bool maskPending;

    /// <summary>While set, the next combo goes here (on the hook thread) instead of triggering anything.</summary>
    private volatile Action<KeyCombo>? recorder;

    /// <summary>
    /// Captures the next key combo, Win combos included, for the shortcut recorder. The key is
    /// swallowed. <paramref name="onCombo"/> runs on the hook's thread and must return quickly.
    /// </summary>
    public void Record(Action<KeyCombo> onCombo) => recorder = onCombo;

    public void CancelRecording() => recorder = null;

    /// <summary>Replaces the combos the hook swallows. Safe from any thread.</summary>
    public void SetBindings(IEnumerable<(KeyCombo Combo, GlideAction Action)> combos) =>
        bindings = combos.DistinctBy(b => b.Combo).ToFrozenDictionary(b => b.Combo, b => b.Action);

    public void Start()
    {
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;
        thread = new Thread(() =>
        {
            try
            {
                threadId = PInvoke.GetCurrentThreadId();
                callback = Callback;
                hook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, callback, (HINSTANCE)(nint)PInvoke.GetModuleHandle((string?)null).DangerousGetHandle(), 0);
                Native.ThrowIfFailed(!hook.IsNull, "Installing the keyboard hook");
                mouseCallback = MouseCallback;
                mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, mouseCallback, (HINSTANCE)(nint)PInvoke.GetModuleHandle((string?)null).DangerousGetHandle(), 0);
                if (mouseHook.IsNull) Log.Error("Couldn't install the mouse hook; the wheel won't scroll the strip");
            }
            catch (Exception e)
            {
                failure = e;
                return;
            }
            finally
            {
                ready.Set();
            }
            while (PInvoke.GetMessage(out var message, HWND.Null, 0, 0) > 0)
            {
                PInvoke.TranslateMessage(message);
                PInvoke.DispatchMessage(message);
            }
            PInvoke.UnhookWindowsHookEx(hook);
            if (!mouseHook.IsNull) PInvoke.UnhookWindowsHookEx(mouseHook);
        })
        {
            IsBackground = true,
            Name = "Glide keyboard hook",
            Priority = ThreadPriority.Highest,
        };
        thread.Start();
        ready.Wait();
        if (failure is not null) throw failure;
    }

    private LRESULT Callback(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code < 0) return PInvoke.CallNextHookEx(hook, code, wParam, lParam);
        var info = (KBDLLHOOKSTRUCT*)lParam.Value;
        // Glide's own keys (the Start mask) pass through. Other injected input is matched, so key
        // remappers like PowerToys Keyboard Manager keep working.
        if (info->dwExtraInfo == InjectedMarker) return PInvoke.CallNextHookEx(hook, code, wParam, lParam);

        var message = (uint)wParam.Value;
        var down = message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
        var key = KeyCode.ByPosition(info->vkCode, info->scanCode, info->flags.HasFlag(KBDLLHOOKSTRUCT_FLAGS.LLKHF_EXTENDED));

        if (info->vkCode is Win32.VK_LWIN or Win32.VK_RWIN)
        {
            if (!down && maskPending)
            {
                maskPending = false;
                // Swallow the release and replay it after an unassigned key, so Windows sees Win
                // was used in a combo and keeps the Start menu closed.
                ReleaseWinWithMask((ushort)info->vkCode);
                return (LRESULT)1;
            }
            return PInvoke.CallNextHookEx(hook, code, wParam, lParam);
        }
        if (IsModifier(info->vkCode)) return PInvoke.CallNextHookEx(hook, code, wParam, lParam);

        if (down)
        {
            var combo = new KeyCombo(key, CurrentModifiers());
            if (recorder is { } record)
            {
                recorder = null;
                swallowedKeys.Add(key);
                if (combo.Modifiers.HasFlag(KeyModifiers.Win)) maskPending = true;
                record(combo);
                return (LRESULT)1;
            }
            if (bindings.TryGetValue(combo, out var action))
            {
                swallowedKeys.Add(key);
                if (combo.Modifiers.HasFlag(KeyModifiers.Win)) maskPending = true;
                onAction(action);
                return (LRESULT)1;
            }
        }
        else if (swallowedKeys.Remove(key))
        {
            return (LRESULT)1;
        }
        return PInvoke.CallNextHookEx(hook, code, wParam, lParam);
    }

    private const uint WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;

    /// <summary>
    /// The horizontal wheel scrolls the strip (always, or only with Win held, per the setting);
    /// Win + the vertical wheel does too, for mice without one. Like the keyboard callback, this
    /// only decides and swallows; the scrolling happens on the UI thread.
    /// </summary>
    private LRESULT MouseCallback(int code, WPARAM wParam, LPARAM lParam)
    {
        var message = (uint)wParam.Value;
        if (code < 0 || !ScrollEnabled || message is not (WM_MOUSEWHEEL or WM_MOUSEHWHEEL))
            return PInvoke.CallNextHookEx(mouseHook, code, wParam, lParam);
        var info = (MSLLHOOKSTRUCT*)lParam.Value;
        if (info->dwExtraInfo == InjectedMarker) return PInvoke.CallNextHookEx(mouseHook, code, wParam, lParam);

        var win = IsDown(Win32.VK_LWIN) || IsDown(Win32.VK_RWIN);
        var horizontal = message == WM_MOUSEHWHEEL;
        var takes = horizontal ? HorizontalWheel == WheelMode.Always || (HorizontalWheel == WheelMode.WithWin && win) : win;
        if (!takes) return PInvoke.CallNextHookEx(mouseHook, code, wParam, lParam);

        var delta = (short)(info->mouseData >> 16) / 120.0;
        // Tilting right or turning the thumb wheel forward goes right; the vertical wheel turned
        // down (towards you) goes right too, like scrolling down a page.
        onScroll(horizontal ? delta : -delta);
        if (win) maskPending = true;
        return (LRESULT)1;
    }

    private static bool IsModifier(uint key) => key is Win32.VK_SHIFT or Win32.VK_CONTROL or Win32.VK_MENU
        or Win32.VK_LSHIFT or Win32.VK_RSHIFT or Win32.VK_LCONTROL or Win32.VK_RCONTROL or Win32.VK_LMENU or Win32.VK_RMENU;

    private static bool IsDown(int key) => (PInvoke.GetAsyncKeyState(key) & 0x8000) != 0;

    private static KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsDown(Win32.VK_LWIN) || IsDown(Win32.VK_RWIN)) modifiers |= KeyModifiers.Win;
        if (IsDown(Win32.VK_CONTROL)) modifiers |= KeyModifiers.Control;
        if (IsDown(Win32.VK_MENU)) modifiers |= KeyModifiers.Alt;
        if (IsDown(Win32.VK_SHIFT)) modifiers |= KeyModifiers.Shift;
        return modifiers;
    }

    private static void ReleaseWinWithMask(ushort winKey)
    {
        Span<INPUT> inputs =
        [
            Key(Win32.VK_NONAME, KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY),
            Key(Win32.VK_NONAME, KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
            Key(winKey, KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP),
        ];
        PInvoke.SendInput(inputs, sizeof(INPUT));
    }

    private static INPUT Key(ushort key, KEYBD_EVENT_FLAGS flags) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)key, dwFlags = flags, dwExtraInfo = InjectedMarker } },
    };

    public void Dispose()
    {
        if (thread is null) return;
        ScrollEnabled = false;
        PInvoke.PostThreadMessage(threadId, Win32.WM_QUIT, default, default);
        thread.Join(TimeSpan.FromSeconds(2));
        thread = null;
    }
}

/// <summary>When the horizontal wheel scrolls the strip instead of the app under the pointer.</summary>
public enum WheelMode
{
    /// <summary>Always: the thumb wheel is for moving along the strip.</summary>
    Always,
    /// <summary>Only while Win is held; otherwise apps get horizontal scrolling as usual.</summary>
    WithWin,
    Off,
}
