namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// Per-locale coverage summary for the Translate workspace top selector (contracts/translations.md
/// § GET /translations). <see cref="KeysTranslated"/> counts source keys with a non-blank target
/// value; <see cref="CoveragePercent"/> is that as a percentage of <see cref="KeysCount"/> (the
/// source key total). The English source locale is always 100%.
/// </summary>
public sealed record LocaleCoverage(
    string Locale,
    int CoveragePercent,
    int KeysCount,
    int KeysTranslated,
    DateTimeOffset? UpdatedAt);
