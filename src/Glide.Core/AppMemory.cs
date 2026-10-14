using System.Text.Json;

namespace Glide.Core;

/// <summary>Where an app's windows were last tiled.</summary>
/// <param name="Monitor">The monitor's device name; null if unknown.</param>
/// <param name="WidthFraction">A width the user set by hand, as a share of the screen; null for a preset.</param>
public sealed record AppPlacement(string? Monitor, int Workspace, int WidthIndex, bool IsFullWidth = false, double? WidthFraction = null);

/// <summary>
/// What Glide remembers about each app, so its windows come back where the user keeps them: the
/// monitor, workspace and width of its column, and where on the strip it sits among other apps.
/// Learned from the layout as it changes; apps keep their place while closed.
/// </summary>
public sealed class AppMemory
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>The last placement of each app, by app ID (case-insensitive).</summary>
    public Dictionary<string, AppPlacement> Apps { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Apps left to right on each strip, keyed by <see cref="Key"/>.</summary>
    public Dictionary<string, List<string>> Order { get; init; } = [];

    public static string Key(string? monitor, int workspace) => $"{monitor ?? ""}|{workspace}";

    /// <summary>One strip as it is now: its monitor, workspace, and each column's apps.</summary>
    /// <param name="Columns">Per column: its width settings and the app of each window (null if unknown).</param>
    public sealed record Strip(string? Monitor, int Workspace, double ViewportWidth, IReadOnlyList<(Column Column, IReadOnlyList<string?> Apps)> Columns);

    /// <summary>
    /// Updates the memory from every strip at once. Apps on screen take their current placement;
    /// closed apps keep theirs and their place among the others; an app that moved leaves the strip it was on.
    /// </summary>
    public void Learn(IReadOnlyList<Strip> strips)
    {
        var present = new Dictionary<string, List<string>>();
        foreach (var strip in strips)
        {
            var sequence = new List<string>();
            foreach (var (column, apps) in strip.Columns)
            {
                foreach (var app in apps)
                {
                    if (app is null) continue;
                    if (!sequence.Contains(app, StringComparer.OrdinalIgnoreCase)) sequence.Add(app);
                    var fraction = column.CustomWidth is { } custom && strip.ViewportWidth > 0 ? custom / strip.ViewportWidth : (double?)null;
                    Apps[app] = new AppPlacement(strip.Monitor, strip.Workspace, column.WidthIndex, column.IsFullWidth, fraction);
                }
            }
            present[Key(strip.Monitor, strip.Workspace)] = sequence;
        }

        // Where each app is now, to drop it from the strips it left.
        var home = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, apps) in present)
            foreach (var app in apps) home[app] = key;

        foreach (var key in present.Keys.Union(Order.Keys).ToList())
        {
            var current = present.GetValueOrDefault(key) ?? [];
            var previous = Order.GetValueOrDefault(key) ?? [];
            var merged = Merge(current, previous.Where(app => !home.TryGetValue(app, out var at) || at == key).ToList());
            if (merged.Count == 0) Order.Remove(key);
            else Order[key] = merged;
        }
    }

    /// <summary>
    /// The current order, with apps known from before (but not open now) put back after the app
    /// that preceded them last time.
    /// </summary>
    private static List<string> Merge(List<string> current, List<string> previous)
    {
        var result = new List<string>(current);
        for (var i = 0; i < previous.Count; i++)
        {
            var app = previous[i];
            if (result.Contains(app, StringComparer.OrdinalIgnoreCase)) continue;
            var after = -1;
            for (var j = i - 1; j >= 0 && after < 0; j--)
                after = result.FindIndex(a => string.Equals(a, previous[j], StringComparison.OrdinalIgnoreCase));
            result.Insert(after + 1, app);
        }
        return result;
    }

    public AppPlacement? Lookup(string? app) => app is not null && Apps.TryGetValue(app, out var placement) ? placement : null;

    /// <summary>
    /// Where a new column for <paramref name="app"/> belongs on a strip whose columns hold
    /// <paramref name="columnApps"/>: before the first column of an app that came after it last
    /// time. Null when the app has no remembered place there.
    /// </summary>
    public int? InsertionIndex(string? monitor, int workspace, string? app, IReadOnlyList<string?> columnApps)
    {
        if (app is null || !Order.TryGetValue(Key(monitor, workspace), out var order)) return null;
        var position = order.FindIndex(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase));
        if (position < 0) return null;
        for (var i = 0; i < columnApps.Count; i++)
        {
            var other = columnApps[i] is { } name ? order.FindIndex(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) : -1;
            if (other > position) return i;
        }
        return columnApps.Count;
    }

    /// <summary>Sorts items into their apps' remembered order on a strip; unknown apps keep their order, after the known ones.</summary>
    public IReadOnlyList<T> Arrange<T>(string? monitor, int workspace, IReadOnlyList<T> items, Func<T, string?> appOf)
    {
        if (!Order.TryGetValue(Key(monitor, workspace), out var order)) return items;
        int Position(T item) => appOf(item) is { } app && order.FindIndex(a => string.Equals(a, app, StringComparison.OrdinalIgnoreCase)) is var i and >= 0 ? i : int.MaxValue;
        return [.. items.Select((item, index) => (item, index)).OrderBy(p => Position(p.item)).ThenBy(p => p.index).Select(p => p.item)];
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AppMemory FromJson(string json)
    {
        var memory = JsonSerializer.Deserialize<AppMemory>(json, JsonOptions) ?? new AppMemory();
        // JSON gives a case-sensitive dictionary; app IDs compare without case.
        return new AppMemory { Apps = new Dictionary<string, AppPlacement>(memory.Apps, StringComparer.OrdinalIgnoreCase), Order = memory.Order };
    }
}
