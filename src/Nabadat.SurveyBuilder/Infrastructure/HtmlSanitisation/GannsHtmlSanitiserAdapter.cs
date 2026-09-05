using Ganss.Xss;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation;
using Nabadat.SurveyBuilder.Application.HtmlSanitisation.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.HtmlSanitisation;

/// <summary>
/// Ganss.Xss (<c>HtmlSanitizer</c> v9) implementation of <see cref="IHtmlSanitiser"/> (research.md
/// §1). Failure-secure: the sanitiser's default tag/attribute sets are cleared and only the
/// policy's allowlist is added back, so <c>script</c>/<c>iframe</c>/<c>object</c>/<c>embed</c>/
/// <c>style</c>/<c>link</c>/<c>meta</c> and every <c>on*</c> handler are dropped by virtue of not
/// being allowed. Inline CSS is removed entirely. URL schemes are restricted to the policy's set
/// (<c>https</c>/<c>mailto</c>/<c>tel</c>); <c>data:</c> is permitted only for <c>data:image/*</c>
/// (all other <c>data:</c>, plus <c>http:</c> and <c>javascript:</c>, are stripped).
/// </summary>
public sealed class GannsHtmlSanitiserAdapter : IHtmlSanitiser
{
    public SanitisedResult Sanitise(string input, SanitiserPolicyVersion policyVersion)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new SanitisedResult(input ?? string.Empty, WasModified: false);
        }

        var sanitiser = Build(policyVersion);

        // WasModified = the sanitiser dropped at least one node/attribute/style/URL.
        var modified = false;
        sanitiser.RemovingTag += (_, _) => modified = true;
        sanitiser.RemovingAttribute += (_, _) => modified = true;
        sanitiser.RemovingStyle += (_, _) => modified = true;
        sanitiser.RemovingAtRule += (_, _) => modified = true;
        sanitiser.FilterUrl += (_, e) =>
        {
            if (!IsAllowedUrl(e.OriginalUrl))
            {
                e.SanitizedUrl = null; // strip the URL (and, for required-URL attributes, the attribute)
                modified = true;
            }
        };

        var clean = sanitiser.Sanitize(input);
        return new SanitisedResult(clean, modified);
    }

    private static HtmlSanitizer Build(SanitiserPolicyVersion policy)
    {
        var sanitiser = new HtmlSanitizer();

        sanitiser.AllowedTags.Clear();
        foreach (var tag in policy.AllowedTags)
        {
            sanitiser.AllowedTags.Add(tag);
        }

        sanitiser.AllowedAttributes.Clear();
        foreach (var attribute in policy.AllowedAttributes)
        {
            sanitiser.AllowedAttributes.Add(attribute);
        }

        // No inline CSS and no data-* attributes — the design system supplies styling via classes.
        sanitiser.AllowedCssProperties.Clear();
        sanitiser.AllowedAtRules.Clear();
        sanitiser.AllowDataAttributes = false;

        sanitiser.AllowedSchemes.Clear();
        foreach (var scheme in policy.AllowedSchemes)
        {
            sanitiser.AllowedSchemes.Add(scheme);
        }

        // Permit the `data` scheme so a data:image/* logo survives; FilterUrl (above) rejects every
        // other data: URL. http / javascript are absent from the allowlist and stripped by Ganss.
        sanitiser.AllowedSchemes.Add("data");

        return sanitiser;
    }

    /// <summary>
    /// A URL is allowed when it is NOT a <c>data:</c> URL (scheme allowlisting handles those),
    /// or when it is specifically a <c>data:image/*</c> URL. Non-image <c>data:</c> URLs are
    /// rejected here.
    /// </summary>
    private static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        return !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
    }
}
