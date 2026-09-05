namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// A flat translation bundle for one locale — the key namespace mirrors
/// <see cref="TranslatableStringExtractor"/> output (research.md §10). The English source bundle
/// (<see cref="TranslatableStringExtractor.SourceLocale"/>) is produced by the extractor; target-locale
/// bundles are the stored <c>survey_translations.keys</c> map.
/// </summary>
public sealed record TranslationBundle(string Locale, IReadOnlyDictionary<string, string> Keys);
