namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-11 (Tenant / Organisation Settings)</b> to resolve the
/// tenant's design-guideline tokens for a survey rendered in <c>Inherited</c> appearance mode
/// (research.md §4.3, F4). Published-interface only.
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-11 (which does
/// not exist under <c>src/</c> yet) and wired in the host composition root (see TODO-M01-006).</para>
/// </summary>
public interface ITenantDesignGuidelinesReader
{
    /// <summary>Returns the current tenant's design-guideline tokens.</summary>
    Task<TenantDesignGuidelines> GetDesignGuidelinesAsync(CancellationToken ct = default);
}
