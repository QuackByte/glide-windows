using System.Globalization;

namespace Glide.Core;

/// <summary>
/// Glide's words in the user's language: English, or Spanish when Windows is set to Spanish.
/// Text is written in English where it's used, wrapped in <see cref="T(string)"/>; the English is
/// the key to its translation. A test checks that every wrapped string has one.
/// </summary>
public static partial class Strings
{
    /// <summary>"en" or "es". Follows Windows' display language unless settings.json says otherwise.</summary>
    public static string Language { get; private set; } = Pick(null);

    /// <param name="setting">"auto" (or null) follows Windows; "en" and "es" choose.</param>
    public static void UseLanguage(string? setting) => Language = Pick(setting);

    private static string Pick(string? setting) => setting?.ToLowerInvariant() switch
    {
        "en" => "en",
        "es" => "es",
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en",
    };

    /// <summary>The culture to format dates and numbers in.</summary>
    public static CultureInfo Culture => CultureInfo.GetCultureInfo(Language == "es" ? "es-ES" : "en-GB");

    /// <summary>The text in the user's language.</summary>
    public static string T(string english) => Language == "es" && spanish.TryGetValue(english, out var text) ? text : english;

    /// <summary>The text in the user's language, with {0}, {1}… filled in.</summary>
    public static string T(string english, params object[] values) => string.Format(Culture, T(english), values);

    /// <summary>Every translation, for the test that checks none is missing.</summary>
    public static IReadOnlyDictionary<string, string> Spanish => spanish;
}
