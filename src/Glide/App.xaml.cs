using Glide.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Glide;

/// <summary>
/// Glide runs from the notification area; there are no windows yet (the settings window and the
/// overview come in later milestones), so the app is the host plus WinUI's message loop.
/// </summary>
public partial class App : Application
{
    private GlideHost? host;
    private Mutex? instance;

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
        if (Environment.GetCommandLineArgs().Contains("--quit"))
        {
            GlideHost.QuitRunningInstance();
            Exit();
            return;
        }
        instance = new Mutex(initiallyOwned: true, @"Local\Glide.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Exit();
            return;
        }
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
        AppDomain.CurrentDomain.ProcessExit += (_, _) => host?.Dispose();
    }
}
