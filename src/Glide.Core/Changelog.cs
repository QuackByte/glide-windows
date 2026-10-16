using System.Globalization;
using System.Text.RegularExpressions;

namespace Glide.Core;

/// <summary>
/// CHANGELOG.md, read for the "What's new" window: "## 1.2.0 — 2026-10-14" starts a release,
/// "### New" a section, and "- " an entry. Text before the first release is ignored.
/// </summary>
public sealed partial class Changelog
{
    public sealed record Section(string Title, IReadOnlyList<string> Entries);

    /// <summary>A release; <see cref="Summary"/> holds paragraphs that aren't in a section.</summary>
    public sealed record Release(Version Version, DateOnly? Date, IReadOnlyList<string> Summary, IReadOnlyList<Section> Sections);

    /// <summary>Newest first, as written.</summary>
    public IReadOnlyList<Release> Releases { get; }

    private Changelog(IReadOnlyList<Release> releases) => Releases = releases;

    [GeneratedRegex(@"^##\s+v?(?<version>\d+\.\d+(\.\d+)?)\s*(?:[—–-]\s*(?<date>\d{4}-\d{2}-\d{2}))?\s*$")]
    private static partial Regex ReleaseHeading();

    public static Changelog Parse(string markdown)
    {
        var releases = new List<Release>();
        Version? version = null;
        DateOnly? date = null;
        var summary = new List<string>();
        var sections = new List<Section>();
        string? sectionTitle = null;
        var entries = new List<string>();

        void EndSection()
        {
            if (sectionTitle is not null && entries.Count > 0) sections.Add(new Section(sectionTitle, [.. entries]));
            sectionTitle = null;
            entries.Clear();
        }

        void EndRelease()
        {
            EndSection();
            if (version is not null) releases.Add(new Release(version, date, [.. summary], [.. sections]));
            version = null;
            summary.Clear();
            sections.Clear();
        }

        foreach (var raw in markdown.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (ReleaseHeading().Match(line) is { Success: true } heading)
            {
                EndRelease();
                version = Version.Parse(heading.Groups["version"].Value);
                date = DateOnly.TryParseExact(heading.Groups["date"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
            }
            else if (version is null)
            {
                continue;
            }
            else if (line.StartsWith("### "))
            {
                EndSection();
                sectionTitle = line[4..].Trim();
            }
            else if (line.TrimStart().StartsWith("- "))
            {
                var entry = line.TrimStart()[2..].Trim();
                if (sectionTitle is null) summary.Add(entry);
                else entries.Add(entry);
            }
            else if (line.Length > 0)
            {
                // A wrapped entry or a paragraph.
                var text = line.Trim();
                if (sectionTitle is not null && entries.Count > 0) entries[^1] += " " + text;
                else if (sectionTitle is null) summary.Add(text);
            }
        }
        EndRelease();
        return new Changelog(releases);
    }

    /// <summary>
    /// This changelog (English, the source of truth for which releases exist) with each release
    /// replaced by <paramref name="translation"/>'s when that has the same version; a release it
    /// lacks stays as is. Order and versions are always this changelog's.
    /// </summary>
    public Changelog WithTranslation(Changelog translation)
    {
        var translated = new Dictionary<Version, Release>();
        foreach (var release in translation.Releases) translated.TryAdd(Normalize(release.Version), release);
        return new Changelog([.. Releases.Select(r => translated.TryGetValue(Normalize(r.Version), out var t) ? t with { Version = r.Version, Date = r.Date } : r)]);
    }

    /// <summary>
    /// The releases someone who last saw <paramref name="seen"/> hasn't seen, up to and including
    /// <paramref name="current"/>, newest first. With no <paramref name="seen"/>, just the current one.
    /// </summary>
    public IReadOnlyList<Release> Since(Version? seen, Version current)
    {
        var now = Normalize(current);
        var since = seen is null ? null : Normalize(seen);
        return [.. Releases.Where(r => Normalize(r.Version) <= now && (since is null ? Normalize(r.Version) == now : Normalize(r.Version) > since))];
    }

    /// <summary>1.2 and 1.2.0 are the same version.</summary>
    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
