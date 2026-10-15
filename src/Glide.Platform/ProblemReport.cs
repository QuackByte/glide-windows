using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Glide.Platform;

/// <summary>
/// Report a Problem: a zip of Glide's log and settings to attach to a GitHub issue, which opens
/// already filled in with the version and the PC. Nothing is sent anywhere; the user attaches the
/// zip themselves. Window titles are taken out of the log first, since they can be private.
/// </summary>
internal static partial class ProblemReport
{
    /// <summary>Writes the zip to the temp folder, shows it in Explorer, and opens a new issue in the browser.</summary>
    public static void Open(string repository)
    {
        var system = SystemSummary();
        var path = Path.Combine(Path.GetTempPath(), $"Glide-report-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Add(zip, "system.txt", system);
            if (Read(Log.FilePath) is { } log) Add(zip, "glide.log", WithoutTitles(log));
            if (Read(Settings.FilePath) is { } settings) Add(zip, "settings.json", settings);
            if (Read(Path.Combine(Settings.Directory, "layouts.json")) is { } layouts) Add(zip, "layouts.json", layouts);
        }
        Log.Info($"Problem report written to {path}");

        var body = $"""
            **What happened?**


            **What did you expect to happen?**


            **How can it be made to happen again?**


            ---
            {system.ReplaceLineEndings(" · ").TrimEnd(' ', '·')}

            Please attach `{Path.GetFileName(path)}` from the folder that just opened: Glide's log and settings, with window titles taken out.
            """;
        Start("explorer.exe", $"/select,\"{path}\"");
        Start($"https://github.com/{repository}/issues/new?body={Uri.EscapeDataString(body)}", null);
    }

    private static string SystemSummary()
    {
        var monitors = Native.Monitors();
        var lines = new StringBuilder();
        lines.AppendLine($"Glide {GlideHost.Version.ToString(3)} ({RuntimeInformation.ProcessArchitecture})");
        lines.AppendLine($"Windows {Environment.OSVersion.Version}");
        lines.AppendLine($"{monitors.Count} monitor(s): {string.Join(", ", monitors.Select(m => $"{m.Bounds.Width}×{m.Bounds.Height} at {m.Scale * 100:0}%"))}");
        lines.AppendLine(Native.IsSelfElevated ? "Running as administrator" : "Not running as administrator");
        return lines.ToString();
    }

    [GeneratedRegex("""(?<id>\d+) "[^"\r\n]*" \(""")]
    private static partial Regex WindowTitle();

    /// <summary>The log names windows as 12345 "Title" (app.exe); the titles go, the rest stays.</summary>
    internal static string WithoutTitles(string log) => WindowTitle().Replace(log, "${id} \"…\" (");

    private static string? Read(string path)
    {
        try
        {
            // The log may be written to at the same moment.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static void Start(string target, string? arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target, arguments ?? "") { UseShellExecute = true });
        }
        catch (Exception e)
        {
            Log.Error($"Couldn't open {target}", e);
        }
    }
}
