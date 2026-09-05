using Nabadat.SurveyBuilder.Application.Translations;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Resolved locale bundle view (contracts/translations.md § GET/PUT /translations/{locale}).
/// <see cref="Keys"/> carries every source key resolved to its target value or English fallback;
/// <see cref="MissingKeys"/> lists source keys the target still lacks. The row's ETag is returned in
/// the <c>ETag</c> header (weak, <c>W/"{row_version}"</c>), not in this body.
/// </summary>
public sealed record TranslationBundleView(
    string Locale,
    IReadOnlyDictionary<string, string> Keys,
    IReadOnlyList<string> MissingKeys)
{
    public static TranslationBundleView From(ResolvedTranslationBundle bundle) =>
        new(bundle.Locale, bundle.Keys, bundle.MissingKeys);
}
