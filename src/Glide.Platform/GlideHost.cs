using System.Diagnostics;
using Glide.Core;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Glide.Platform;

/// <summary>
/// Glide without its UI framework: the tray icon, the keyboard hook and the window manager.
/// Create it on the UI thread, which must run a message loop. The settings window talks to Glide
/// through this class.
/// </summary>
public sealed class GlideHost : IDisposable
{
    /// <summary>Actions that need an app UI that doesn't exist yet; Windows keeps those shortcuts.</summary>
    public static IReadOnlySet<GlideAction> Unavailable { get; } = new HashSet<GlideAction>();

    private readonly Action<Action> post;
    private readonly Action quit;
    private readonly MessageWindow window;
    private readonly TrayIcon tray;
    private readonly KeyboardHook keyboard;
    private readonly WindowManager manager;
    private Settings settings;
    private readonly FileSystemWatcher? settingsWatcher;
    private readonly Process? watchdog;
    private bool reloadPending;
    private bool disposed;

    /// <param name="post">Runs an action on the UI thread; called from the keyboard hook's thread.</param>
    /// <param name="quit">Ends the app after Glide has restored every window.</param>
    public GlideHost(Action<Action> post, Action quit)
    {
        this.post = post;
        this.quit = quit;
        settings = Settings.Load();
        Log.Info($"Glide starting (pid {Environment.ProcessId}, {(IsElevated ? "elevated" : "not elevated")})");

        window = new MessageWindow();
        manager = new WindowManager(window, settings);
        tray = new TrayIcon(window);
        keyboard = new KeyboardHook(action => this.post(() => Run(action)), notches => this.post(() => Scroll(notches)));

        window.MessageReceived = (message, wParam, lParam) =>
        {
            switch (message)
            {
                case MessageWindow.QuitMessage:
                    Log.Info("Asked to quit by another copy of Glide (--quit)");
                    Quit();
                    return true;
                case MessageWindow.ShowSettingsMessage:
                    SettingsRequested?.Invoke();
                    return true;
            }
            if (message is Win32.WM_DISPLAYCHANGE || (message == Win32.WM_SETTINGCHANGE && wParam.Value == 0x2F /* SPI_SETWORKAREA */))
                manager.OnDisplayChanged();
            return tray.Handle(message, wParam, lParam);
        };
        tray.ToggleRequested += manager.Toggle;
        tray.SettingsRequested += () => SettingsRequested?.Invoke();
        tray.QuitRequested += () =>
        {
            Log.Info("Quit from the tray menu");
            Quit();
        };
        manager.TabBarsChanged += bars => TabBarsChanged?.Invoke(bars);
        manager.WorkspaceAnnounced += announcement => WorkspaceAnnounced?.Invoke(announcement);
        manager.StateChanged += () =>
        {
            keyboard.ScrollEnabled = manager.IsEnabled;
            tray.Update(manager.ActiveWorkspaceNumber, manager.IsEnabled);
            StateChanged?.Invoke();
        };

        keyboard.Start();
        ApplyKeymap();
        manager.Enable();
        keyboard.ScrollEnabled = manager.IsEnabled;
        settingsWatcher = WatchSettingsFile();
        watchdog = Watchdog.Launch();
    }

    /// <summary>Picks up edits to settings.json made by hand, a moment after they're saved.</summary>
    private FileSystemWatcher? WatchSettingsFile()
    {
        try
        {
            Directory.CreateDirectory(Settings.Directory);
            var watcher = new FileSystemWatcher(Settings.Directory, Path.GetFileName(Settings.FilePath))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            FileSystemEventHandler changed = (_, _) => post(ScheduleReload);
            watcher.Changed += changed;
            watcher.Created += changed;
            watcher.Renamed += (_, _) => post(ScheduleReload);
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception e)
        {
            Log.Error("Can't watch the settings file", e);
            return null;
        }
    }

    private void ScheduleReload()
    {
        if (reloadPending || disposed) return;
        reloadPending = true;
        // Editors write in several steps; read once they're done.
        window.StartTimer(3, 300, () =>
        {
            window.StopTimer(3);
            reloadPending = false;
            if (disposed || !File.Exists(Settings.FilePath)) return;
            var loaded = Settings.Load();
            // Our own saves come back here too; only act on real changes.
            if (loaded.ToJson() == settings.ToJson()) return;
            Log.Info("Settings file changed; applying it");
            settings = loaded;
            ApplyKeymap();
            manager.ApplySettings(settings);
            SettingsChanged?.Invoke();
        });
    }

    /// <summary>The user asked for the settings window (tray menu, or launching Glide again).</summary>
    public event Action? SettingsRequested;

    /// <summary>The overview shortcut was pressed: open the overview, or close it if it's open.</summary>
    public event Action? OverviewToggleRequested;

    /// <summary>Where tab bars go now; an empty list hides them.</summary>
    public event Action<IReadOnlyList<TabBar>>? TabBarsChanged;

    /// <summary>For the on-screen display: the workspace changed, or a column moved to another.</summary>
    public event Action<WorkspaceAnnouncement>? WorkspaceAnnounced;

    /// <summary>While the overview is open, Glide's other shortcuts do nothing.</summary>
    public bool OverviewOpen { get; set; }

    public OverviewData? MakeOverview() => manager.MakeOverview();

    /// <summary>Goes to a tiled window, switching workspace if needed.</summary>
    public void GoTo(WindowId window) => manager.GoTo(window);

    /// <summary>The window in front, so the overview can give focus back when closed without a choice.</summary>
    public static WindowId ForegroundWindow => Native.Id(Native.Foreground);

    public static void Focus(WindowId window) => Native.Focus(Native.Hwnd(window));

    /// <summary>The settings file was edited outside the settings window.</summary>
    public event Action? SettingsChanged;

    /// <summary>Tiling was turned on or off, or the workspace changed.</summary>
    public event Action? StateChanged;

    public bool IsElevated => Native.IsSelfElevated;

    public bool TilingEnabled
    {
        get => manager.IsEnabled;
        set
        {
            if (value) manager.Enable();
            else manager.Disable();
        }
    }

    /// <summary>A copy of the current settings, to edit and pass to <see cref="ApplySettings"/>.</summary>
    public Settings Settings => settings.Clone();

    /// <summary>Saves the settings and applies them at once.</summary>
    public void ApplySettings(Settings newSettings)
    {
        settings = newSettings.Clone();
        try
        {
            settings.Save();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't save settings", e);
        }
        ApplyKeymap();
        manager.ApplySettings(settings);
    }

    /// <summary>
    /// Captures the next key combo for the shortcut recorder; Glide's own shortcuts are off until
    /// it arrives. <paramref name="onCombo"/> runs on the UI thread.
    /// </summary>
    public void RecordShortcut(Action<KeyCombo> onCombo)
    {
        keyboard.SetBindings([]);
        keyboard.Record(combo => post(() =>
        {
            ApplyKeymap();
            onCombo(combo);
        }));
    }

    public void CancelRecording()
    {
        keyboard.CancelRecording();
        ApplyKeymap();
    }

    /// <summary>Apps with windows open now, for adding rules: executable name and a window title.</summary>
    public IReadOnlyList<(string AppId, string Title)> RunningApps() =>
        [.. Native.TopLevelWindows()
            .Select(Native.Inspect)
            .Where(f => f.IsCandidate && f.AppId is not null)
            .GroupBy(f => f.AppId!, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.First().Title))
            .OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Relaunches Glide as administrator, so it can tile elevated apps' windows and see keys while
    /// they're focused. Returns false if the user declined the UAC prompt.
    /// </summary>
    public bool RestartAsAdministrator()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--replace") { UseShellExecute = true, Verb = "runas" });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        Log.Info("Restarting as administrator");
        Quit();
        return true;
    }

    /// <summary>Brings one of Glide's own windows to the front, past Windows' focus-stealing rules.</summary>
    public static unsafe void BringToFront(nint hwnd) => Native.Focus(new HWND((void*)hwnd));

    public static void OpenInEditor(string path)
    {
        if (path == Settings.FilePath) Settings.EnsureFileExists();
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void Run(GlideAction action)
    {
        if (disposed) return;
        if (action == GlideAction.ToggleOverview)
        {
            if (manager.IsEnabled || OverviewOpen) OverviewToggleRequested?.Invoke();
            return;
        }
        if (OverviewOpen && action != GlideAction.ToggleTiling) return;
        try
        {
            manager.Perform(action);
        }
        catch (Exception e)
        {
            Log.Error($"{action} failed", e);
        }
    }

    private void Scroll(double notches)
    {
        if (disposed || OverviewOpen) return;
        try
        {
            manager.ScrollStrip(notches);
        }
        catch (Exception e)
        {
            Log.Error("Scrolling failed", e);
        }
    }

    private void ApplyKeymap()
    {
        keyboard.HorizontalWheel = settings.HorizontalWheel;
        var keymap = settings.Keymap;
        keyboard.SetBindings(keymap.Bindings
            .Where(b => !Unavailable.Contains(b.Action))
            // Given back to Windows: combos that replace a Windows feature stay Windows'.
            .Where(b => settings.OverrideWindowsShortcuts || ShortcutPolicy.ReplacedWindowsFeature(b.Combo) is null)
            .Select(b => (b.Combo, b.Action)));
        tray.ToggleShortcut = keymap.Combo(GlideAction.ToggleTiling) is { } toggle ? KeyLabels.Describe(toggle) : "";
    }

    // Other instances

    /// <summary>Asks an already running Glide to restore its windows and quit. Returns false if none runs.</summary>
    public static bool QuitRunningInstance() => Signal(MessageWindow.QuitMessage);

    /// <summary>Asks an already running Glide to show its settings. Returns false if none runs.</summary>
    public static bool ShowSettingsInRunningInstance() => Signal(MessageWindow.ShowSettingsMessage);

    private static bool Signal(uint message)
    {
        var other = PInvoke.FindWindow(MessageWindow.ClassName, null);
        if (other == HWND.Null) return false;
        // The running copy brings its settings window forward; let it.
        PInvoke.AllowSetForegroundWindow(Native.ProcessId(other));
        return PInvoke.PostMessage(other, message, default, default);
    }

    /// <summary>A clean exit: the watchdog has nothing to recover.</summary>
    private void StopWatchdog()
    {
        try
        {
            if (watchdog is { HasExited: false }) watchdog.Kill();
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        watchdog?.Dispose();
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
        settingsWatcher?.Dispose();
        StopWatchdog();
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
