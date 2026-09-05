namespace Nabadat.SurveyBuilder.Application.HtmlSanitisation.Interfaces;

/// <summary>
/// Server-side rich-text sanitiser for the welcome / thank-you HTML (Q3, research.md §1). The
/// security boundary is here (architecture-constitution Article 5) — any client-side cleaning is
/// UX only. The concrete implementation (Ganss.Xss) lives in Infrastructure behind this port.
/// </summary>
public interface IHtmlSanitiser
{
    /// <summary>
    /// Sanitises <paramref name="input"/> against the given allowlist <paramref name="policyVersion"/>
    /// and reports whether anything was stripped. The caller persists the applied
    /// <see cref="SanitiserPolicyVersion.PolicyVersion"/> alongside the result for the audit trail.
    /// </summary>
    SanitisedResult Sanitise(string input, SanitiserPolicyVersion policyVersion);
}
