namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// The resolved view of a target locale (contracts/translations.md § GET /translations/{locale}):
/// every source key resolved to its target value or English fallback (<see cref="Keys"/>), plus the
/// source keys still absent from the target (<see cref="MissingKeys"/> — the workspace coverage hint).
/// </summary>
public sealed record ResolvedTranslationBundle(
    string Locale,
    IReadOnlyDictionary<string, string> Keys,
    IReadOnlyList<string> MissingKeys);
