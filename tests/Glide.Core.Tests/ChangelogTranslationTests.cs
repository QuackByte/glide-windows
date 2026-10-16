using System.Text.RegularExpressions;

namespace Glide.Core.Tests;

/// <summary>
/// changelog/CHANGELOG.&lt;language&gt;.md must say what CHANGELOG.md says: the same releases, with the
/// same sections, entries and links. Only the words differ. Fails when a release is added in English
/// without being translated, so translate it before tagging.
/// </summary>
public partial class ChangelogTranslationTests
{
    public static TheoryData<string> Languages => new() { "es", "fr", "de", "ja", "zh", "ko", "pt" };

    [GeneratedRegex(@"https?://[^\s)\]>]+")]
    private static partial Regex Url();

    private static string Describe(Changelog.Release r) => r.Version + " (" + (r.Date?.ToString("yyyy-MM-dd") ?? "no date") + ")";

    private static IEnumerable<string> Texts(Changelog.Release r) => r.Summary.Concat(r.Sections.SelectMany(s => s.Entries));

    private static string[] Links(Changelog.Release r) => [.. Texts(r).SelectMany(t => Url().Matches(t).Select(m => m.Value)).Distinct().Order(StringComparer.Ordinal)];

    [Fact]
    public void TheRepositoryHasATranslationForEveryLanguage()
    {
        // Strings.Languages is the app's list; the theory above must cover every non-English one.
        Assert.Equal(
            Strings.Languages.Select(l => l.Code).Where(c => c != "en").Order(StringComparer.Ordinal),
            Languages.Select(l => (string)l[0]).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void MatchesTheEnglishChangelog(string language)
    {
        var name = $"CHANGELOG.{language}.md";
        var path = Path.Combine(AppContext.BaseDirectory, "changelog", name);
        Assert.True(File.Exists(path), $"changelog/{name} is missing. Every language needs a translated changelog.");

        var english = Changelog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md")));
        var translated = Changelog.Parse(File.ReadAllText(path));
        var problems = new List<string>();

        var englishHeadings = english.Releases.Select(Describe).ToArray();
        var translatedHeadings = translated.Releases.Select(Describe).ToArray();
        if (!englishHeadings.SequenceEqual(translatedHeadings))
        {
            problems.Add($"{name}: release headings differ (version and date, in order).\n  CHANGELOG.md: {string.Join(", ", englishHeadings)}\n  {name}: {string.Join(", ", translatedHeadings)}");
        }
        else
        {
            foreach (var (en, tr) in english.Releases.Zip(translated.Releases))
            {
                var at = $"{name} {Describe(en)}";
                if (en.Summary.Count != tr.Summary.Count)
                    problems.Add($"{at}: {tr.Summary.Count} summary paragraphs, CHANGELOG.md has {en.Summary.Count}.");
                if (en.Sections.Count != tr.Sections.Count)
                {
                    problems.Add($"{at}: {tr.Sections.Count} sections, CHANGELOG.md has {en.Sections.Count}.");
                }
                else
                {
                    foreach (var (es, ts) in en.Sections.Zip(tr.Sections))
                    {
                        if (es.Entries.Count != ts.Entries.Count)
                            problems.Add($"{at}, section \"{ts.Title}\" (English \"{es.Title}\"): {ts.Entries.Count} entries, CHANGELOG.md has {es.Entries.Count}.");
                    }
                }
                var missing = Links(en).Except(Links(tr)).ToArray();
                var extra = Links(tr).Except(Links(en)).ToArray();
                if (missing.Length > 0) problems.Add($"{at}: links missing from the translation: {string.Join(", ", missing)}");
                if (extra.Length > 0) problems.Add($"{at}: links not in CHANGELOG.md: {string.Join(", ", extra)}");
            }
        }

        Assert.True(problems.Count == 0, "\n" + string.Join("\n", problems));
    }
}
