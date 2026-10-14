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
            host = new GlideHost(action => queue.TryEnqueue(() => action()), Exit);
        }
        catch (Exception e)
        {
            Log.Error("Glide couldn't start", e);
            host?.Dispose();
            Exit();
            return;
        }
        host.SettingsRequested += ShowSettings;
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
