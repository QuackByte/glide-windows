using System.Diagnostics;
using Glide.Core;

namespace Glide.Platform;

/// <summary>
/// Glide without its UI framework: the tray icon, the keyboard hook and the window manager.
/// Create it on the UI thread, which must run a message loop.
/// </summary>
public sealed class GlideHost : IDisposable
{
    /// <summary>Actions that need an app UI that doesn't exist yet; Windows keeps those shortcuts.</summary>
    private static readonly HashSet<GlideAction> unavailable = [GlideAction.ToggleOverview];

    private readonly Action<Action> post;
    private readonly Action quit;
    private readonly MessageWindow window;
    private readonly TrayIcon tray;
    private readonly KeyboardHook keyboard;
    private readonly WindowManager manager;
    private Keymap keymap;
    private bool disposed;

    /// <param name="post">Runs an action on the UI thread; called from the keyboard hook's thread.</param>
    /// <param name="quit">Ends the app after Glide has restored every window.</param>
    public GlideHost(Action<Action> post, Action quit)
    {
        this.post = post;
        this.quit = quit;
        var settings = Settings.Load();
        keymap = settings.Keymap;
        Log.Info($"Glide starting (pid {Environment.ProcessId}, {(Native.IsSelfElevated ? "elevated" : "not elevated")})");

        window = new MessageWindow();
        manager = new WindowManager(window, settings);
        tray = new TrayIcon(window);
        keyboard = new KeyboardHook(action => this.post(() => Run(action)));

        window.MessageReceived = (message, wParam, lParam) =>
        {
            if (message == MessageWindow.QuitMessage)
            {
                Quit();
                return true;
            }
            if (message is Win32.WM_DISPLAYCHANGE || (message == Win32.WM_SETTINGCHANGE && wParam.Value == 0x2F /* SPI_SETWORKAREA */))
                manager.OnDisplayChanged();
            return tray.Handle(message, wParam, lParam);
        };
        tray.ToggleRequested += manager.Toggle;
        tray.OpenSettingsRequested += OpenSettings;
        tray.ReloadSettingsRequested += ReloadSettings;
        tray.QuitRequested += Quit;
        manager.StateChanged += () => tray.Update(manager.ActiveWorkspaceNumber, manager.IsEnabled);

        keyboard.Start();
        ApplyKeymap();
        manager.Enable();
    }

    private void Run(GlideAction action)
    {
        if (disposed) return;
        try
        {
            manager.Perform(action);
        }
        catch (Exception e)
        {
            Log.Error($"{action} failed", e);
        }
    }

    private void ApplyKeymap()
    {
        keyboard.SetBindings(keymap.Bindings.Where(b => !unavailable.Contains(b.Action)).Select(b => (b.Combo, b.Action)));
        tray.ToggleShortcut = keymap.Combo(GlideAction.ToggleTiling)?.ToString() ?? "";
    }

    private void OpenSettings()
    {
        Settings.EnsureFileExists();
        Process.Start(new ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
    }

    private void ReloadSettings()
    {
        var settings = Settings.Load();
        keymap = settings.Keymap;
        ApplyKeymap();
        manager.ApplySettings(settings);
        Log.Info("Settings reloaded");
    }

    /// <summary>Asks an already running Glide to restore its windows and quit. Returns false if none runs.</summary>
    public static bool QuitRunningInstance()
    {
        var other = Windows.Win32.PInvoke.FindWindow(MessageWindow.ClassName, null);
        return other != Windows.Win32.Foundation.HWND.Null && Windows.Win32.PInvoke.PostMessage(other, MessageWindow.QuitMessage, default, default);
    }

    public void Quit()
    {
        Dispose();
        quit();
    }

    /// <summary>Restores every window, then removes the hooks and the tray icon. Safe to call twice.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            manager.Dispose();
        }
        finally
        {
            keyboard.Dispose();
            tray.Dispose();
            window.Dispose();
            Log.Info("Glide stopped");
        }
    }
}
