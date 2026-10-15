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

    [Fact]
    public void EveryStringHasASpanishTranslation()
    {
        var strings = WrappedStrings();
        Assert.True(strings.Count > 150, $"Only {strings.Count} strings found; is the source where the test looks?");
        var missing = strings.Where(s => !Strings.Spanish.ContainsKey(s)).ToList();
        Assert.True(missing.Count == 0, $"No Spanish for: {string.Join(" | ", missing)}");
    }

    [Fact]
    public void TranslationsKeepTheirPlaceholdersAndAreAllUsed()
    {
        foreach (var (english, spanish) in Strings.Spanish)
        {
            Assert.Equal(Placeholder().Matches(english).Select(m => m.Value).Order(), Placeholder().Matches(spanish).Select(m => m.Value).Order());
            Assert.False(string.IsNullOrWhiteSpace(spanish), english);
        }
        // Strings that are no longer used leave the table too.
        var used = WrappedStrings().ToHashSet();
        var unused = Strings.Spanish.Keys.Where(k => !used.Contains(k)).ToList();
        Assert.True(unused.Count == 0, $"Not used any more: {string.Join(" | ", unused)}");
    }

    [Fact]
    public void SpanishIsUsedWhenChosen()
    {
        try
        {
            Strings.UseLanguage("es");
            Assert.Equal("Mover columna al escritorio 3", GlideAction.MoveColumnToWorkspace3.Title());
            Assert.Equal("Escritorios", ActionGroup.Workspaces.Title());
        }
        finally
        {
            Strings.UseLanguage("en");
        }
        Assert.Equal("Move Column to Workspace 3", GlideAction.MoveColumnToWorkspace3.Title());
    }
}
