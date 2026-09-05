using Nabadat.SurveyBuilder.Application.Translations;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One locale's coverage row for the workspace top selector (contracts/translations.md § GET
/// /translations). Wrapped in <see cref="TranslationLocalesResponse"/>.
/// </summary>
public sealed record LocaleSummary(
    string Locale,
    int CoveragePercent,
    int KeysCount,
    int KeysTranslated,
    DateTimeOffset? UpdatedAt)
{
    public static LocaleSummary From(LocaleCoverage coverage) =>
        new(coverage.Locale, coverage.CoveragePercent, coverage.KeysCount, coverage.KeysTranslated, coverage.UpdatedAt);
}
