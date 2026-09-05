namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// A resolved locale bundle together with the stored row's ETag counter — the controller sets
/// <c>ETag: W/"{RowVersion}"</c> from it. <see cref="RowVersion"/> is 0 when no row is stored for the
/// locale yet (a not-yet-translated target, or the derived English source view).
/// </summary>
public sealed record TranslationBundleResult(ResolvedTranslationBundle Bundle, int RowVersion);
