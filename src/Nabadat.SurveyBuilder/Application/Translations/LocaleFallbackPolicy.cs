namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// Resolves one translation key for a target locale, falling back to the English source when the
/// target has no (non-blank) value (BR-3.2 — "translations may be completed later", research.md §10).
/// A saved-but-blank target value counts as untranslated and falls back, so a blank Arabic string
/// never blanks out the rendered survey.
/// </summary>
public sealed class LocaleFallbackPolicy
{
    /// <summary>The source locale every key falls back to.</summary>
    public const string SourceLocale = "en";

    public string? Resolve(IReadOnlyDictionary<string, TranslationBundle> bundlesByLocale, string locale, string key)
    {
        if (bundlesByLocale.TryGetValue(locale, out var target)
            && target.Keys.TryGetValue(key, out var value)
            && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (bundlesByLocale.TryGetValue(SourceLocale, out var source)
            && source.Keys.TryGetValue(key, out var english))
        {
            return english;
        }

        return null;
    }
}
