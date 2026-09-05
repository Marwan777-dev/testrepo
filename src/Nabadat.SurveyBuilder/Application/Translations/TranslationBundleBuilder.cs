namespace Nabadat.SurveyBuilder.Application.Translations;

/// <summary>
/// Assembles the resolved view a GET returns (contracts/translations.md § GET /translations/{locale}):
/// every <b>source</b> key resolved to its target value or English fallback, plus the source keys the
/// target is still missing. Applies <see cref="LocaleFallbackPolicy"/> across the whole source key set
/// (policy = resolve-one-key primitive; builder = resolve-the-whole-bundle). The view is driven by the
/// source key set, so a stale target key (e.g. for a deleted question) is dropped.
/// <para>Created to resolve TODO-M01-021 — <c>TranslationBundleBuilder</c> is a named US6 unit-under-test
/// (T204) that tasks.md T208–T217 never assigned an implementation task; its contract is pinned by
/// <c>TranslationBundleBuilderTests</c>.</para>
/// </summary>
public sealed class TranslationBundleBuilder
{
    private readonly LocaleFallbackPolicy _fallbackPolicy;

    public TranslationBundleBuilder(LocaleFallbackPolicy fallbackPolicy) => _fallbackPolicy = fallbackPolicy;

    public ResolvedTranslationBundle Build(TranslationBundle source, TranslationBundle target)
    {
        var bundlesByLocale = new Dictionary<string, TranslationBundle>
        {
            [LocaleFallbackPolicy.SourceLocale] = source,
            [target.Locale] = target,
        };

        var resolved = new Dictionary<string, string>();
        var missingKeys = new List<string>();

        foreach (var key in source.Keys.Keys)
        {
            var value = _fallbackPolicy.Resolve(bundlesByLocale, target.Locale, key);
            if (value is not null)
            {
                resolved[key] = value;
            }

            if (!target.Keys.TryGetValue(key, out var targetValue) || string.IsNullOrWhiteSpace(targetValue))
            {
                missingKeys.Add(key);
            }
        }

        return new ResolvedTranslationBundle(target.Locale, resolved, missingKeys);
    }
}
