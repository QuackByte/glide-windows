using System.Globalization;

namespace Glide.Core;

/// <summary>
/// Glide's words in the user's language: English, or one of the translations below when Windows is
/// set to that language. Text is written in English where it's used, wrapped in
/// <see cref="T(string)"/>; the English is the key to its translation. A test checks that every
/// wrapped string has one in every language.
/// </summary>
public static partial class Strings
{
    /// <summary>The languages Glide can show, with each one's name in itself and the culture its dates and numbers use.</summary>
    public static readonly IReadOnlyList<(string Code, string Name, string Culture)> Languages =
    [
        ("en", "English", "en-GB"),
        ("es", "Español", "es-ES"),
        ("fr", "Français", "fr-FR"),
        ("de", "Deutsch", "de-DE"),
        ("ja", "日本語", "ja-JP"),
        ("zh", "简体中文", "zh-CN"),
        ("ko", "한국어", "ko-KR"),
        ("pt", "Português (Brasil)", "pt-BR"),
    ];

    /// <summary>"en", "es", "fr", "de", "ja", "zh", "ko" or "pt". Follows Windows' display language unless settings.json says otherwise.</summary>
    public static string Language { get; private set; } = Pick(null);

    /// <param name="setting">"auto" (or null) follows Windows; a language code chooses.</param>
    public static void UseLanguage(string? setting) => Language = Pick(setting);

    private static string Pick(string? setting) => Pick(setting, CultureInfo.CurrentUICulture);

    /// <summary>
    /// The language for a settings.json value and a Windows display language. Chinese is only
    /// picked for Simplified; Traditional Chinese stays English.
    /// </summary>
    internal static string Pick(string? setting, CultureInfo windows)
    {
        var chosen = setting?.Trim().ToLowerInvariant();
        if (chosen is not null && Languages.Any(l => l.Code == chosen)) return chosen;
        var name = windows.Name.ToLowerInvariant();
        return windows.TwoLetterISOLanguageName.ToLowerInvariant() switch
        {
            "es" => "es",
            "fr" => "fr",
            "de" => "de",
            "ja" => "ja",
            "ko" => "ko",
            "pt" => "pt",
            "zh" when IsSimplifiedChinese(name) => "zh",
            _ => "en",
        };
    }

    private static bool IsSimplifiedChinese(string name) => name.Contains("hans") || name is "zh" or "zh-cn" or "zh-sg";

    /// <summary>The culture to format dates and numbers in.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Languages.First(l => l.Code == Language).Culture);

    /// <summary>The text in the user's language.</summary>
    public static string T(string english) =>
        Language != "en" && Translations.TryGetValue(Language, out var table) && table.TryGetValue(english, out var text) ? text : english;

    /// <summary>The text in the user's language, with {0}, {1}… filled in.</summary>
    public static string T(string english, params object[] values) => string.Format(Culture, T(english), values);

    /// <summary>Every translation by language code (everything but English), for the test that checks none is missing.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Translations => translations ??=
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["es"] = spanish,
            ["fr"] = french,
            ["de"] = german,
            ["ja"] = japanese,
            ["zh"] = chinese,
            ["ko"] = korean,
            ["pt"] = portuguese,
        };

    // Built on first use: each language's table is a static field in its own file, and the order
    // static fields of a partial class are initialized in across files isn't defined.
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? translations;

    /// <summary>The Spanish translations.</summary>
    public static IReadOnlyDictionary<string, string> Spanish => spanish;
}
