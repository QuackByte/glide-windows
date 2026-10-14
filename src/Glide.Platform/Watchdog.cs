using System.Diagnostics;
using Glide.Core;

namespace Glide.Platform;

/// <summary>
/// Glide parks columns at the screen edges and hides other workspaces' windows in a corner; if it
/// crashed they would stay there. So Glide starts a copy of itself in watchdog mode that waits for
/// it to exit. A clean quit stops the watchdog first. Any other exit makes it bring every window
/// back on screen, and after a real crash (not "End task") it relaunches Glide, which re-tiles
/// from the saved layout.
/// </summary>
public static class Watchdog
{
    public const string Argument = "--watchdog";
    private static readonly string relaunchStamp = Path.Combine(Settings.Directory, "last-crash-relaunch");
    /// <summary>A crash loop shouldn't relaunch forever.</summary>
    private static readonly TimeSpan relaunchInterval = TimeSpan.FromSeconds(60);

    /// <summary>Starts the watchdog for this process. Kill it before a clean exit.</summary>
    public static Process? Launch()
    {
        try
        {
            return Process.Start(new ProcessStartInfo(Environment.ProcessPath!, $"{Argument} {Environment.ProcessId}") { UseShellExecute = false });
        }
        catch (Exception e)
        {
            Log.Error("Couldn't start the crash watchdog", e);
            return null;
        }
    }

    /// <summary>The watchdog process: waits for Glide to exit, then recovers. Returns the exit code.</summary>
    public static int Run(int parentId)
    {
        var exitCode = WaitForExit((uint)parentId);
        Log.Info($"Watchdog: Glide (pid {parentId}) exited without quitting (code {(exitCode is { } code ? $"0x{code:X}" : "unknown")}); restoring windows");
        RestoreWindows();
        if (IsCrash(exitCode) && MayRelaunch()) Relaunch();
        return 0;
    }

    /// <summary>Waits for a process to end and returns its exit code; null if it can't be watched.</summary>
    private static unsafe uint? WaitForExit(uint pid)
    {
        var process = Windows.Win32.PInvoke.OpenProcess(
            Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_SYNCHRONIZE | Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
            false, pid);
        // Already gone before we started watching.
        if (process.IsNull) return null;
        try
        {
            Windows.Win32.PInvoke.WaitForSingleObject(process, uint.MaxValue);
            uint code;
            return Windows.Win32.PInvoke.GetExitCodeProcess(process, &code) ? code : null;
        }
        finally
        {
            Windows.Win32.PInvoke.CloseHandle(process);
        }
    }

    /// <summary>
    /// Unhandled exceptions and fail-fasts end with an NTSTATUS-style code; "End task" and
    /// Stop-Process end with 1 (or -1), which isn't a crash.
    /// </summary>
    private static bool IsCrash(uint? exitCode) => exitCode is { } code && code is not (0 or 1 or 0xFFFFFFFF);

    private static bool MayRelaunch()
    {
        try
        {
            if (File.Exists(relaunchStamp) && DateTime.UtcNow - File.GetLastWriteTimeUtc(relaunchStamp) < relaunchInterval) return false;
            Directory.CreateDirectory(Settings.Directory);
            File.WriteAllText(relaunchStamp, DateTime.UtcNow.ToString("O"));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static void Relaunch()
    {
        Log.Info("Watchdog: relaunching Glide after a crash");
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        }
        catch (Exception e)
        {
            Log.Error("Watchdog: relaunch failed", e);
        }
    }

    /// <summary>
    /// Moves every app window that's off screen back on, the way turning tiling off does, and puts
    /// back the borders the focus ring colored.
    /// </summary>
    public static void RestoreWindows()
    {
        var (workArea, scale) = Native.PrimaryDisplay();
        var frames = new List<(WindowId, Rect)>();
        foreach (var hwnd in Native.TopLevelWindows())
        {
            if (!Native.Inspect(hwnd).IsCandidate || Native.VisibleFrame(hwnd) is not { } frame) continue;
            Native.SetBorderColor(hwnd, null);
            frames.Add((Native.Id(hwnd), frame));
        }
        var restored = RestorePlanner.RestoredFrames(frames, workArea, inset: Math.Round(12 * scale));
        var moved = 0;
        foreach (var (id, frame) in restored)
        {
            var hwnd = Native.Hwnd(id);
            if (Native.VisibleFrame(hwnd) == frame) continue;
            Native.SetVisibleFrame(hwnd, frame, Native.BordersOf(hwnd));
            moved++;
        }
        Log.Info($"Watchdog: moved {moved} windows back on screen");
    }
}
