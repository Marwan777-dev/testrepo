using System.Collections.Frozen;

namespace Nabadat.SurveyBuilder.Application.HtmlSanitisation;

/// <summary>
/// A versioned, immutable HTML sanitiser allowlist (Q3, research.md §1). Every survey save records
/// the applied <see cref="PolicyVersion"/> on the row so the sanitisation is auditable and
/// versioned. The active policy is <see cref="V1"/>.
/// </summary>
public sealed record SanitiserPolicyVersion(
    int PolicyVersion,
    FrozenSet<string> AllowedTags,
    FrozenSet<string> AllowedAttributes,
    FrozenSet<string> AllowedSchemes,
    FrozenSet<string> StrippedTags)
{
    /// <summary>
    /// The v1 allowlist enumerated in research.md §1: Full HTML5 minus <c>script</c>/<c>iframe</c>/
    /// <c>object</c>/<c>embed</c>/<c>style</c>/<c>link</c>/<c>meta</c>, no <c>on*</c> handlers, and
    /// only <c>https</c>/<c>mailto</c>/<c>tel</c> URL schemes (<c>http</c> and <c>javascript:</c>
    /// stripped; <c>data:</c> only for <c>data:image/*</c>).
    /// </summary>
    public static SanitiserPolicyVersion V1 { get; } = new(
        PolicyVersion: 1,
        AllowedTags: new[]
        {
            "p", "br", "b", "strong", "i", "em", "u",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "ul", "ol", "li", "a", "blockquote", "code", "pre",
            "span", "div", "hr", "img",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        AllowedAttributes: new[]
        {
            "href", "title", "target", "rel", "dir", "lang", "class", "src", "alt",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        AllowedSchemes: new[] { "https", "mailto", "tel" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
        StrippedTags: new[]
        {
            "script", "iframe", "object", "embed", "style", "link", "meta",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase));
}
