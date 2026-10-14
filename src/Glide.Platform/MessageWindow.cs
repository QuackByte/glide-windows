using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Glide.Platform;

/// <summary>
/// A hidden top-level window on the UI thread that receives the tray icon's callbacks, timers and
/// display broadcasts. Not message-only: those don't get TaskbarCreated or WM_DISPLAYCHANGE.
/// </summary>
internal sealed unsafe class MessageWindow : IDisposable
{
    public const string ClassName = "GlideMessageWindow";
    /// <summary>Asks the running Glide to quit (sent by <c>Glide.exe --quit</c>).</summary>
    public const uint QuitMessage = Win32.WM_APP + 2;
    /// <summary>Asks the running Glide to show its settings (sent when Glide is launched again).</summary>
    public const uint ShowSettingsMessage = Win32.WM_APP + 3;
    private readonly WNDPROC procedure;
    private readonly Dictionary<nuint, Action> timers = [];

    public MessageWindow()
    {
        procedure = WindowProcedure;
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
        Handle = PInvoke.CreateWindowEx(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, ClassName, "Glide", WINDOW_STYLE.WS_POPUP, 0, 0, 0, 0,
                                        HWND.Null, null, null, null);
        Native.ThrowIfFailed(Handle != HWND.Null, "Creating the message window");
    }

    public HWND Handle { get; }

    public nint Pointer => (nint)Handle.Value;

    /// <summary>Handles a message; return true to mark it handled.</summary>
    public Func<uint, WPARAM, LPARAM, bool>? MessageReceived { get; set; }

    /// <summary>Calls <paramref name="tick"/> on the UI thread every <paramref name="milliseconds"/> until stopped.</summary>
    public void StartTimer(nuint id, uint milliseconds, Action tick)
    {
        timers[id] = tick;
        PInvoke.SetTimer(Handle, id, milliseconds, null);
    }

    public void StopTimer(nuint id)
    {
        if (timers.Remove(id)) PInvoke.KillTimer(Handle, id);
    }

    private LRESULT WindowProcedure(HWND hwnd, uint message, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (message == Win32.WM_TIMER && timers.TryGetValue(wParam.Value, out var tick))
            {
                tick();
                return default;
            }
            if (MessageReceived?.Invoke(message, wParam, lParam) == true) return default;
        }
        catch (Exception e)
        {
            Log.Error("Message handler failed", e);
        }
        return PInvoke.DefWindowProc(hwnd, message, wParam, lParam);
    }

    public void Dispose()
    {
        foreach (var id in timers.Keys.ToList()) StopTimer(id);
        PInvoke.DestroyWindow(Handle);
    }
}
