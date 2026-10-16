namespace Glide.Core.Tests;

public class ChangelogTests
{
    private const string Sample = """
        # Changelog

        Intro text that isn't a release.

        - Not an entry either.

        ## 0.8.0 — 2026-11-02

        A paragraph about this release.

        ### New

        - **Bold** first entry,
          wrapped onto a second line.
        - Second entry.

        ### Fixes

        - A fix.

        ## 0.7.0 — 2026-10-14

        ### New

        - Updates.

        ## 0.6
        - Old summary entry.
        """;

    [Fact]
    public void ParsesReleasesSectionsAndEntries()
    {
        var log = Changelog.Parse(Sample);
        Assert.Equal(["0.8.0", "0.7.0", "0.6"], log.Releases.Select(r => r.Version.ToString()));

        var latest = log.Releases[0];
        Assert.Equal(new DateOnly(2026, 11, 2), latest.Date);
        Assert.Equal(["A paragraph about this release."], latest.Summary);
        Assert.Equal(["New", "Fixes"], latest.Sections.Select(s => s.Title));
        Assert.Equal(["**Bold** first entry, wrapped onto a second line.", "Second entry."], latest.Sections[0].Entries);

        Assert.Null(log.Releases[2].Date);
        Assert.Equal(["Old summary entry."], log.Releases[2].Summary);
    }

    [Fact]
    public void SinceListsUnseenReleasesUpToTheCurrentOne()
    {
        var log = Changelog.Parse(Sample);
        Assert.Equal(["0.8.0", "0.7.0"], log.Since(new Version(0, 6, 0), new Version(0, 8, 0)).Select(r => r.Version.ToString()));
        Assert.Equal(["0.7.0"], log.Since(new Version(0, 6), new Version(0, 7, 0)).Select(r => r.Version.ToString()));
        Assert.Empty(log.Since(new Version(0, 8, 0), new Version(0, 8, 0)));
    }

    [Fact]
    public void WithoutAPreviousVersionOnlyTheCurrentReleaseShows()
    {
        var log = Changelog.Parse(Sample);
        Assert.Equal(["0.7.0"], log.Since(null, new Version(0, 7, 0, 0)).Select(r => r.Version.ToString()));
        Assert.Empty(log.Since(null, new Version(0, 9, 0)));
    }

    [Fact]
    public void TheRepositoryChangelogParses()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md");
        var log = Changelog.Parse(File.ReadAllText(path));
        Assert.NotEmpty(log.Releases);
        Assert.All(log.Releases, r => Assert.True(r.Summary.Count > 0 || r.Sections.Count > 0, $"{r.Version} has no entries"));
        // Newest first, no duplicates.
        Assert.Equal(log.Releases.Select(r => r.Version).OrderByDescending(v => v), log.Releases.Select(r => r.Version));
    }

    private const string English = """
        ## 0.8.0 — 2026-11-02

        ### New

        - One.
        - Two.

        ## 0.7.0 — 2026-10-14

        ### New

        - Updates.

        ## 0.6.0 — 2026-09-01

        - Old.
        """;

    private static string[] Versions(Changelog log) => [.. log.Releases.Select(r => r.Version.ToString())];

    [Fact]
    public void ATranslatedReleaseReplacesTheEnglishOne()
    {
        var spanish = Changelog.Parse("""
            ## 0.8.0 — 2026-11-02

            ### Novedades

            - Uno.
            - Dos.
            """);
        var merged = Changelog.Parse(English).WithTranslation(spanish);
        Assert.Equal(["Novedades"], merged.Releases[0].Sections.Select(s => s.Title));
        Assert.Equal(["Uno.", "Dos."], merged.Releases[0].Sections[0].Entries);
    }

    [Fact]
    public void AReleaseWithoutATranslationStaysEnglishInPlace()
    {
        var spanish = Changelog.Parse("""
            ## 0.7.0 — 2026-10-14

            ### Novedades

            - Actualizaciones.
            """);
        var merged = Changelog.Parse(English).WithTranslation(spanish);
        Assert.Equal(["0.8.0", "0.7.0", "0.6.0"], Versions(merged));
        Assert.Equal(["New"], merged.Releases[0].Sections.Select(s => s.Title));
        Assert.Equal(["Novedades"], merged.Releases[1].Sections.Select(s => s.Title));
        Assert.Equal(["Old."], merged.Releases[2].Summary);
    }

    [Fact]
    public void VersionsAndOrderComeFromEnglishOnly()
    {
        // 0.9.0 exists only in the translation, and it is listed oldest first.
        var spanish = Changelog.Parse("""
            ## 0.6 — 2026-09-01

            - Viejo.

            ## 0.9.0 — 2026-12-01

            - Futuro.

            ## 0.8.0 — 2026-11-02

            - Uno.
            """);
        var merged = Changelog.Parse(English).WithTranslation(spanish);
        Assert.Equal(["0.8.0", "0.7.0", "0.6.0"], Versions(merged));
        Assert.Equal(["Uno."], merged.Releases[0].Summary);
        Assert.Equal(["Viejo."], merged.Releases[2].Summary);
        Assert.Equal(["0.8.0", "0.7.0"], merged.Since(new Version(0, 6, 0), new Version(0, 9, 0)).Select(r => r.Version.ToString()));
    }

    [Fact]
    public void AnEmptyTranslationChangesNothing()
    {
        var merged = Changelog.Parse(English).WithTranslation(Changelog.Parse(""));
        Assert.Equal(["0.8.0", "0.7.0", "0.6.0"], Versions(merged));
        Assert.Equal(["One.", "Two."], merged.Releases[0].Sections[0].Entries);
    }
}
