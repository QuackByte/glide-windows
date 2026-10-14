using System.Diagnostics;
using Microsoft.Win32;

namespace Glide.Platform;

/// <summary>
/// Starting Glide at sign-in. Normally through the HKCU Run value the installer writes. Running as
/// administrator, through a scheduled task with highest privileges instead: a Run entry can't
/// start an app elevated, and the task does so without a UAC prompt at every sign-in.
/// </summary>
public static class StartupEntry
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Glide";

    /// <summary>Whether Glide starts at sign-in, either way.</summary>
    public static bool IsEnabled
    {
        get => HasRunValue || HasTask;
        set
        {
            if (value)
            {
                if (Native.IsSelfElevated && CreateTask())
                    SetRunValue(false);
                else
                    SetRunValue(true);
            }
            else
            {
                SetRunValue(false);
                if (HasTask) DeleteTask();
            }
        }
    }

    /// <summary>Glide starts at sign-in as administrator.</summary>
    public static bool StartsElevated => HasTask;

    private static bool HasRunValue
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    private static void SetRunValue(bool on)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (on)
            key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(Name, throwOnMissingValue: false);
    }

    private static bool HasTask => Schtasks("/Query", "/TN", Name) == 0;

    private static bool CreateTask()
    {
        // /IT: only in the user's session, when they're signed in; /RL HIGHEST: elevated.
        var created = Schtasks("/Create", "/TN", Name, "/TR", $"\"{Environment.ProcessPath}\"", "/SC", "ONLOGON",
                               "/RU", Environment.UserName, "/IT", "/RL", "HIGHEST", "/F") == 0;
        if (!created) Log.Error("Couldn't create the sign-in task");
        return created;
    }

    private static void DeleteTask()
    {
        if (Schtasks("/Delete", "/TN", Name, "/F") != 0) Log.Error("Couldn't remove the sign-in task (it needs administrator rights)");
    }

    private static int Schtasks(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("schtasks.exe") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit(10_000);
            return process.ExitCode;
        }
        catch (Exception e)
        {
            Log.Error("schtasks failed", e);
            return -1;
        }
    }
}
