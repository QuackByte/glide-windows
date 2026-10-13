using System.Text.Json.Serialization;

namespace Glide.Core;

/// <summary>
/// How Glide treats the windows of one app. Rules apply to windows as they appear; a window you
/// float or tile by hand (Win+Ctrl+V) keeps your choice.
/// </summary>
/// <param name="AppId">The app's executable name, e.g. "notepad.exe" (case-insensitive).</param>
/// <param name="WidthIndex">Index into the width presets for new columns; null uses the default.</param>
/// <param name="Workspace">Zero-based workspace new windows open on; null opens them on the active one.</param>
public sealed record AppRule(string AppId, string AppName, AppRule.RuleMode Mode = AppRule.RuleMode.Automatic, int? WidthIndex = null, int? Workspace = null)
{
    [JsonConverter(typeof(JsonStringEnumConverter<RuleMode>))]
    public enum RuleMode
    {
        /// <summary>Glide decides: standard resizable windows tile, everything else floats.</summary>
        Automatic,
        Float,
        /// <summary>Tiles resizable windows the classifier would float.</summary>
        Tile,
    }

    /// <summary>Settings has a fixed layout and comes and goes, so it floats out of the box.</summary>
    public static IReadOnlyList<AppRule> Defaults { get; } = [new("SystemSettings.exe", "Settings", RuleMode.Float)];
}

/// <summary>The user's rules, keyed by app ID.</summary>
public sealed class AppRules
{
    private readonly List<AppRule> rules = [];

    public AppRules() : this(AppRule.Defaults) { }

    public AppRules(IEnumerable<AppRule> rules)
    {
        // One rule per app; a later duplicate replaces an earlier one.
        foreach (var rule in rules) Set(rule);
    }

    public IReadOnlyList<AppRule> Rules => rules;

    private int IndexOf(string appId) => rules.FindIndex(r => string.Equals(r.AppId, appId, StringComparison.OrdinalIgnoreCase));

    public AppRule? Rule(string? appId)
    {
        if (appId is null) return null;
        var index = IndexOf(appId);
        return index < 0 ? null : rules[index];
    }

    /// <summary>Adds a rule, or replaces the app's existing one in place.</summary>
    public void Set(AppRule rule)
    {
        var index = IndexOf(rule.AppId);
        if (index >= 0)
            rules[index] = rule;
        else
            rules.Add(rule);
    }

    public void Remove(string appId) => rules.RemoveAll(r => string.Equals(r.AppId, appId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Whether a new window should tile. <paramref name="classifiedTileable"/> is Glide's own
    /// judgement; <paramref name="canTile"/> says whether the window could be tiled at all
    /// (resizable, not a dialog).
    /// </summary>
    public static bool ShouldTile(AppRule? rule, bool classifiedTileable, bool canTile, bool userFloating, bool userTiled)
    {
        if (userTiled) return canTile;
        if (userFloating) return false;
        return (rule?.Mode ?? AppRule.RuleMode.Automatic) switch
        {
            AppRule.RuleMode.Float => false,
            AppRule.RuleMode.Tile => canTile,
            _ => classifiedTileable,
        };
    }
}
