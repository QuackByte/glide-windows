using Glide.Platform;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Glide;

/// <summary>
/// Glide's entry point. In watchdog mode it waits for the Glide it watches and never starts
/// WinUI, so it stays small; otherwise it's the usual WinUI startup.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == Watchdog.Argument && int.TryParse(args[1], out var parent))
            return Watchdog.Run(parent);

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
        return 0;
    }
}
