using System.Text.Json;
using System.Text.Json.Serialization;
using Glide.Core;

namespace Glide.Platform;

/// <summary>
/// %APPDATA%\Glide\settings.json. Until the settings window exists (milestone 3), this file is how
/// shortcuts are rebound: <c>"shortcuts": { "FocusColumnLeft": "Win+H", "CenterColumn": "none" }</c>.
/// </summary>
public sealed class Settings
{
    public static string Directory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Glide");
    public static string FilePath { get; } = Path.Combine(Directory, "settings.json");

    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Space between columns and around the screen edge, in pixels at 100% scaling.</summary>
    public double Gap { get; set; } = 12;
    /// <summary>Slide windows into place instead of jumping.</summary>
    public bool Animate { get; set; } = true;
    /// <summary>Overrides of the default shortcuts, by action name. "none" removes one.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = [];
    public List<AppRule> Rules { get; set; } = [.. AppRule.Defaults];

    [JsonIgnore]
    public Keymap Keymap
    {
        get
        {
            var keymap = new Keymap();
            foreach (var (name, value) in Shortcuts)
            {
                if (!Enum.TryParse<GlideAction>(name, ignoreCase: true, out var action) || !Enum.IsDefined(action))
                {
                    Log.Info($"Settings: unknown action \"{name}\"");
                    continue;
                }
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Length == 0)
                    keymap.Set(null, action);
                else if (KeyCombo.Parse(value) is { } combo)
                    keymap.Set(combo, action);
                else
                    Log.Info($"Settings: can't read shortcut \"{value}\" for {name}");
            }
            return keymap;
        }
    }

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath)) return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), json) ?? new Settings();
        }
        catch (Exception e)
        {
            Log.Error($"Couldn't read {FilePath}; using defaults", e);
        }
        return new Settings();
    }

    /// <summary>Writes the defaults if there is no settings file yet, so there's something to edit.</summary>
    public static void EnsureFileExists()
    {
        if (File.Exists(FilePath)) return;
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Settings(), json));
    }
}

/// <summary>A small log at %LOCALAPPDATA%\Glide\glide.log, for diagnosing problems on someone else's PC.</summary>
public static class Log
{
    private static readonly Lock gate = new();
    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glide", "glide.log");

    public static bool Verbose { get; set; } = Environment.GetEnvironmentVariable("GLIDE_VERBOSE") == "1";

    public static void Debug(string message)
    {
        if (Verbose) Write("debug", message);
    }

    public static void Info(string message) => Write("info", message);

    public static void Error(string message, Exception? error = null) => Write("error", error is null ? message : $"{message}: {error}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (gate)
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 2_000_000) info.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
    }
}
