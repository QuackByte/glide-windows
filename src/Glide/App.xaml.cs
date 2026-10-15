using Glide.Core;
using Glide.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Glide;

/// <summary>
/// Glide runs from the notification area. The app is the host plus WinUI's message loop, and the
/// settings window when it's open.
/// </summary>
public partial class App : Application
{
    private GlideHost? host;
    private Mutex? instance;
    private SettingsWindow? settingsWindow;
    private WhatsNewWindow? whatsNewWindow;
    private ShortcutSheetWindow? shortcutSheet;
    private OverviewWindow? overview;
    private TabBars? tabBars;
    private readonly WorkspaceDisplay workspaceDisplay = new();
    /// <summary>Never shown. Keeps the app running when the last visible window closes; Quit closes it.</summary>
    private Window? anchor;

    public App()
    {
        Log.Debug("App starting");
        InitializeComponent();
        // Whatever goes wrong, don't leave windows parked off screen.
        UnhandledException += (_, e) =>
        {
            Log.Error("Unhandled exception", e.Exception);
            host?.Dispose();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Error("Unhandled exception", e.ExceptionObject as Exception);
            host?.Dispose();
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs();
        if (arguments.Contains("--quit"))
        {
            GlideHost.QuitRunningInstance();
            Exit();
            return;
        }

        instance = new Mutex(initiallyOwned: false, @"Local\Glide.SingleInstance");
        // --replace: a copy of Glide restarting this one (e.g. as administrator) waits for it to go.
        if (!TryOwn(instance, arguments.Contains("--replace") ? TimeSpan.FromSeconds(15) : TimeSpan.Zero))
        {
            // Launching Glide again (e.g. from the Start menu) opens the running copy's settings.
            GlideHost.ShowSettingsInRunningInstance();
            Exit();
            return;
        }

        var firstRun = !File.Exists(Settings.FilePath);
        var queue = DispatcherQueue.GetForCurrentThread();
        try
        {
            host = new GlideHost(action => queue.TryEnqueue(() => action()), Quit);
        }
        catch (Exception e)
        {
            Log.Error("Glide couldn't start", e);
            host?.Dispose();
            Exit();
            return;
        }
        anchor = new Window();
        host.SettingsRequested += ShowSettings;
        host.WhatsNewRequested += () => ShowWhatsNew(WhatsNewWindow.History.Releases);
        host.OverviewToggleRequested += ToggleOverview;
        host.ShortcutSheetRequested += ToggleShortcutSheet;
        tabBars = new TabBars(host);
        host.TabBarsChanged += tabBars.Show;
        host.WorkspaceAnnounced += announcement =>
        {
            if (host.Settings.ShowWorkspaceDisplay) workspaceDisplay.Show(announcement);
        };
        host.StateChanged += () =>
        {
            if (!host.TilingEnabled) overview?.Close(goTo: null);
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            Log.Info("Process exiting");
            host?.Dispose();
        };
        if (firstRun)
        {
            // Welcome: show what Glide does and where its shortcuts are, once.
            host.ApplySettings(host.Settings);
            ShowSettings();
        }
        ShowWhatsNewAfterUpdate(firstRun);
    }

    /// <summary>
    /// The first start of a newer version shows what changed since the last one seen. A new install
    /// only remembers its version. Copies from before 0.7 never saved one, so they see this version's.
    /// </summary>
    private void ShowWhatsNewAfterUpdate(bool firstRun)
    {
        if (host is null) return;
        var current = GlideHost.Version;
        var settings = host.Settings;
        Version? seen = Version.TryParse(settings.LastSeenVersion, out var parsed) ? parsed : null;
        if (seen is not null && seen.ToString(3) == current.ToString(3)) return;
        settings.LastSeenVersion = current.ToString(3);
        host.ApplySettings(settings);
        if (firstRun) return;
        var releases = WhatsNewWindow.History.Since(seen, current);
        if (releases.Count > 0) ShowWhatsNew(releases);
    }

    private void ShowWhatsNew(IReadOnlyList<Changelog.Release> releases)
    {
        if (releases.Count == 0) return;
        try
        {
            whatsNewWindow?.Close();
            whatsNewWindow = new WhatsNewWindow(releases);
            whatsNewWindow.Closed += (_, _) => whatsNewWindow = null;
            whatsNewWindow.Activate();
            GlideHost.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(whatsNewWindow));
        }
        catch (Exception e)
        {
            Log.Error("Couldn't show what's new", e);
            whatsNewWindow = null;
        }
    }

    private static bool TryOwn(Mutex mutex, TimeSpan timeout)
    {
        try
        {
            return mutex.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // The previous copy exited without releasing it; it's ours now.
            return true;
        }
    }

    /// <summary>
    /// Ends the app once Glide has put every window back. WinUI keeps running while any of its
    /// windows exist, hidden overlays included, so they're closed first, and the process ends
    /// regardless a moment later.
    /// </summary>
    private void Quit()
    {
        try
        {
            overview?.Close();
            tabBars?.Close();
            workspaceDisplay.Close();
            settingsWindow?.Close();
            whatsNewWindow?.Close();
            shortcutSheet?.Close();
            anchor?.Close();
        }
        catch (Exception e)
        {
            Log.Error("Closing windows on quit failed", e);
        }
        Exit();
        _ = Task.Delay(TimeSpan.FromSeconds(2)).ContinueWith(_ => Environment.Exit(0));
    }

    private void ToggleOverview()
    {
        if (host is null) return;
        try
        {
            overview ??= new OverviewWindow(host);
            if (overview.IsOpen)
                overview.Close(goTo: null);
            else
                overview.Open();
        }
        catch (Exception e)
        {
            Log.Error("Overview failed", e);
            host.OverviewOpen = false;
        }
    }

    /// <summary>Shows every shortcut; its shortcut again closes it.</summary>
    private void ToggleShortcutSheet()
    {
        if (host is null) return;
        if (shortcutSheet is not null)
        {
            shortcutSheet.Close();
            return;
        }
        try
        {
            shortcutSheet = new ShortcutSheetWindow(host);
            shortcutSheet.Closed += (_, _) => shortcutSheet = null;
            shortcutSheet.Activate();
            GlideHost.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(shortcutSheet));
        }
        catch (Exception e)
        {
            Log.Error("Couldn't show the shortcuts", e);
            shortcutSheet = null;
        }
    }

    private void ShowSettings()
    {
        if (host is null) return;
        Log.Debug("Showing settings");
        try
        {
            if (settingsWindow is null)
            {
                settingsWindow = new SettingsWindow(host);
                settingsWindow.Closed += (_, _) => settingsWindow = null;
            }
            settingsWindow.Activate();
        }
        catch (Exception e)
        {
            Log.Error("Couldn't show settings", e);
            settingsWindow = null;
            return;
        }
        // Launched from another process or the tray: make sure it comes to the front.
        GlideHost.BringToFront(WinRT.Interop.WindowNative.GetWindowHandle(settingsWindow));
    }
}
