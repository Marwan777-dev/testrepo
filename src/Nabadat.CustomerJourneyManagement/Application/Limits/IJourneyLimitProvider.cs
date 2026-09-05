namespace Nabadat.CustomerJourneyManagement.Application.Limits;

/// <summary>
/// Supplies the per-tenant journey structural limits (max stages per journey, max touchpoints
/// per stage) that <c>StageService</c> (T025) and <c>TouchpointService</c> (T026) enforce before
/// appending a child. The values originate from M-11 (<c>IM11TenantService.GetJourneyLimits()</c>)
/// and fall back to platform defaults when M-11 is unavailable — that resolution + fallback logic
/// is the concrete <c>JourneyLimitEnforcer</c> (T027). Consumers depend only on this seam so they
/// stay unit-testable without M-11: the unit suite substitutes it with fixed limits.
/// </summary>
public interface IJourneyLimitProvider
{
    /// <summary>
    /// Returns the effective journey limits for the current tenant/request. Never throws for an
    /// unavailable upstream — the implementation falls back to platform defaults.
    /// </summary>
    Task<JourneyLimits> GetLimitsAsync(CancellationToken ct = default);
}

/// <summary>
/// The per-tenant journey structural limits. Defaults are 20 stages per journey and 30 touchpoints
/// per stage (applied by <c>JourneyLimitEnforcer</c> when M-11 cannot supply tenant-specific values).
/// </summary>
/// <param name="MaxStagesPerJourney">Maximum stages a single journey may contain.</param>
/// <param name="MaxTouchpointsPerStage">Maximum touchpoints a single stage may contain.</param>
public sealed record JourneyLimits(int MaxStagesPerJourney, int MaxTouchpointsPerStage);
