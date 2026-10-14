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
        // Keep "Win+←" readable in a file people edit by hand.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Space between columns and around the screen edge, in pixels at 100% scaling.</summary>
    public double Gap { get; set; } = 12;
    /// <summary>Slide windows into place instead of jumping.</summary>
    public bool Animate { get; set; } = true;
    /// <summary>Colors the focused tiled window's border (Windows 11).</summary>
    public bool FocusRing { get; set; } = true;
    /// <summary>The ring's thickness around the focused window, in pixels at 100% scaling.</summary>
    public double FocusRingWidth { get; set; } = 4;
    /// <summary>"accent" for the Windows accent color, or "#RRGGBB".</summary>
    public string FocusRingColor { get; set; } = "accent";
    /// <summary>
    /// Whether Glide's shortcuts may take over combos Windows uses (Snap, Task View, virtual
    /// desktops…). Off gives those back to Windows, like FancyZones' "Override Windows Snap".
    /// </summary>
    public bool OverrideWindowsShortcuts { get; set; } = true;
    /// <summary>When the horizontal wheel (tilt, or a thumb wheel) scrolls the strip. Win + the vertical wheel always does.</summary>
    public WheelMode HorizontalWheel { get; set; } = WheelMode.Always;
    /// <summary>Horizontal three-finger swipes on a precision touchpad glide along the strip.</summary>
    public bool TrackpadSwipe { get; set; } = true;
    /// <summary>New windows (and, after a restart, open ones) go back to their app's usual monitor, workspace, width and place.</summary>
    public bool RememberApps { get; set; } = true;
    /// <summary>Look for a newer Glide twice a day and offer it from the tray.</summary>
    public bool CheckForUpdates { get; set; } = true;
    /// <summary>The GitHub repository ("owner/name") whose releases carry Glide's installers.</summary>
    public string UpdateRepository { get; set; } = "QuackByte/glide-windows";
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
                if (int.TryParse(name, out _) || !Enum.TryParse<GlideAction>(name, ignoreCase: true, out var action) || !Enum.IsDefined(action))
                {
                    Log.Info($"Settings: unknown action \"{name}\"");
                    continue;
                }
                if (value.Equals("none", StringComparison.OrdinalIgnoreCase) || value.Length == 0)
                    keymap.Set(null, action);
                else if (KeyCombo.Parse(value) is not { } combo)
                    Log.Info($"Settings: can't read shortcut \"{value}\" for {name}");
                else if (ShortcutPolicy.Evaluate(combo) is ShortcutPolicy.Verdict.Rejected(var reason))
                    Log.Info($"Settings: {combo} can't be used for {name}: {reason}");
                else
                    keymap.Set(combo, action);
            }
            return keymap;
        }
    }

    /// <summary>Stores a keymap's overrides as readable shortcuts ("Win+J", "none").</summary>
    public void SetKeymap(Keymap keymap) =>
        Shortcuts = keymap.Overrides.ToDictionary(p => p.Key.ToString(), p => p.Value?.ToString() ?? "none");

    public string ToJson() => JsonSerializer.Serialize(this, json);

    public Settings Clone() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this, json), json)!;

    public void Save()
    {
        System.IO.Directory.CreateDirectory(Directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, json));
        File.Move(temporary, FilePath, overwrite: true);
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
