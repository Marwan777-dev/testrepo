namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Request body for PUT /translations/{locale} (contracts/translations.md). A partial or full bundle;
/// keys not present are preserved unchanged (merge semantics). Keys must correspond to current source
/// strings — unknown keys yield <c>400 translation.key.unknown</c>.
/// </summary>
public sealed record PutTranslationBundleRequest(IReadOnlyDictionary<string, string> Keys)
{
    public IReadOnlyDictionary<string, string> ToBundle() =>
        Keys ?? new Dictionary<string, string>();
}
