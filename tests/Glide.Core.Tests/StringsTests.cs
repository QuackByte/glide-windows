using System.Globalization;
using System.Text.RegularExpressions;

namespace Glide.Core.Tests;

/// <summary>Switching language affects every test running at the time, so these run alone.</summary>
[CollectionDefinition(nameof(StringsTests), DisableParallelization = true)]
[Collection(nameof(StringsTests))]
public partial class StringsTests
{
    [GeneratedRegex("""(?<![\w.])T\("((?:[^"\\]|\\.)*)"[,)]""")]
    private static partial Regex Wrapped();

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    /// <summary>Every T("…") in the app, read from the source.</summary>
    private static IReadOnlyList<string> WrappedStrings()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException("src");
        return [.. Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => Wrapped().Matches(File.ReadAllText(file)).Select(m => Regex.Unescape(m.Groups[1].Value)))
            .Distinct()];
    }

    /// <summary>Every language but English, as xUnit data.</summary>
    public static TheoryData<string> Translated
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var language in Strings.Translations.Keys) data.Add(language);
            return data;
        }
    }

    [Fact]
    public void EveryLanguageHasATranslationTable()
    {
        Assert.Equal(Strings.Languages.Select(l => l.Code).Where(c => c != "en").Order(), Strings.Translations.Keys.Order());
    }

    [Theory]
    [MemberData(nameof(Translated))]
    public void EveryStringIsTranslated(string language)
    {
        var strings = WrappedStrings();
        Assert.True(strings.Count > 150, $"Only {strings.Count} strings found; is the source where the test looks?");
        var missing = strings.Where(s => !Strings.Translations[language].ContainsKey(s)).ToList();
        Assert.True(missing.Count == 0, $"No translation to {language} for: {string.Join(" | ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Translated))]
    public void TranslationsKeepTheirPlaceholdersAndAreAllUsed(string language)
    {
        var table = Strings.Translations[language];
        foreach (var (english, translation) in table)
        {
            Assert.Equal(Placeholder().Matches(english).Select(m => m.Value).Order(), Placeholder().Matches(translation).Select(m => m.Value).Order());
            Assert.False(string.IsNullOrWhiteSpace(translation), $"{language}: {english}");
        }
        // Strings that are no longer used leave the table too.
        var used = WrappedStrings().ToHashSet();
        var unused = table.Keys.Where(k => !used.Contains(k)).ToList();
        Assert.True(unused.Count == 0, $"{language}: not used any more: {string.Join(" | ", unused)}");
    }

    [Theory]
    [MemberData(nameof(Translated))]
    public void ATranslationIsUsedWhenChosen(string language)
    {
        var table = Strings.Translations[language];
        if (table.Count == 0) return; // not translated yet; the completeness test says so
        var (english, translation) = table.First();
        try
        {
            Strings.UseLanguage(language);
            Assert.Equal(language, Strings.Language);
            Assert.Equal(translation, Strings.T(english));
        }
        finally
        {
            Strings.UseLanguage("en");
        }
        Assert.Equal(english, Strings.T(english));
    }

    [Fact]
    public void SpanishIsUsedWhenChosen()
    {
        try
        {
            Strings.UseLanguage("es");
            Assert.Equal("Mover ventana al escritorio 3", GlideAction.MoveColumnToWorkspace3.Title());
            Assert.Equal("Escritorios", ActionGroup.Workspaces.Title());
        }
        finally
        {
            Strings.UseLanguage("en");
        }
        Assert.Equal("Move Window to Workspace 3", GlideAction.MoveColumnToWorkspace3.Title());
    }

    [Fact]
    public void EveryLanguageHasACulture()
    {
        foreach (var (code, name, culture) in Strings.Languages)
        {
            try
            {
                Strings.UseLanguage(code);
                Assert.Equal(culture, Strings.Culture.Name);
                Assert.False(string.IsNullOrWhiteSpace(name));
            }
            finally
            {
                Strings.UseLanguage("en");
            }
        }
    }

    [Theory]
    [InlineData("auto", "es-ES", "es")]
    [InlineData(null, "fr-CA", "fr")]
    [InlineData("auto", "de-AT", "de")]
    [InlineData("auto", "ja-JP", "ja")]
    [InlineData("auto", "ko-KR", "ko")]
    [InlineData("auto", "pt-PT", "pt")]
    [InlineData("auto", "pt-BR", "pt")]
    [InlineData("auto", "zh-CN", "zh")]
    [InlineData("auto", "zh-SG", "zh")]
    [InlineData("auto", "zh-Hans", "zh")]
    [InlineData("auto", "zh-Hans-CN", "zh")]
    [InlineData("auto", "zh-TW", "en")]
    [InlineData("auto", "zh-HK", "en")]
    [InlineData("auto", "zh-MO", "en")]
    [InlineData("auto", "zh-Hant", "en")]
    [InlineData("auto", "zh-Hant-TW", "en")]
    [InlineData("auto", "en-US", "en")]
    [InlineData("auto", "it-IT", "en")]
    [InlineData("en", "fr-FR", "en")]
    [InlineData("FR", "en-US", "fr")]
    [InlineData("zh", "zh-TW", "zh")]
    [InlineData("klingon", "de-DE", "de")]
    public void LanguageFollowsTheSettingThenWindows(string? setting, string windows, string expected)
    {
        Assert.Equal(expected, Strings.Pick(setting, CultureInfo.GetCultureInfo(windows)));
    }
}
