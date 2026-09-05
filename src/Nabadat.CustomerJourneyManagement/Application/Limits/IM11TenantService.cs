namespace Nabadat.CustomerJourneyManagement.Application.Limits;

/// <summary>
/// The M-16-owned consumer slice of M-11's published tenant service — exactly the one capability
/// M-16 needs: the current tenant's journey structural limits (max stages per journey, max
/// touchpoints per stage). <see cref="JourneyLimitEnforcer"/> calls it once per request and maps
/// the result onto <see cref="JourneyLimits"/>.
/// <para>
/// M-11 is not present in this working tree, so — per the module precedent (no shared
/// <c>Nabadat.Platform.Contracts</c> project; M-16 declares the upstream port it consumes in-module,
/// just as <c>IM17EventPublisher</c> does for M-17) — M-16 declares only the narrow port it depends
/// on. When M-11 lands it supplies the concrete adapter wired in the composition root (T028).
/// </para>
/// <para>
/// The contract is allowed to throw when M-11 is unavailable (network failure or an open
/// circuit-breaker). <see cref="JourneyLimitEnforcer"/> owns the resilience: it catches that,
/// logs a warning, and falls back to platform defaults so a limit-lookup outage never blocks a
/// journey edit.
/// </para>
/// </summary>
public interface IM11TenantService
{
    /// <summary>
    /// Returns the current tenant's journey limits. May throw when M-11 is unreachable or its
    /// circuit-breaker is open — callers that need a non-throwing result use
    /// <see cref="IJourneyLimitProvider"/> (implemented by <see cref="JourneyLimitEnforcer"/>),
    /// which applies the platform-default fallback.
    /// </summary>
    Task<JourneyLimitsDto> GetJourneyLimitsAsync(CancellationToken ct = default);
}

/// <summary>
/// The journey limits as returned by M-11's tenant service. Carries the same two values as
/// <see cref="JourneyLimits"/>; kept as a distinct DTO so the M-11 wire shape stays separate from
/// M-16's internal limit type (<see cref="JourneyLimitEnforcer"/> maps one to the other).
/// </summary>
/// <param name="MaxStagesPerJourney">Tenant's maximum stages per journey.</param>
/// <param name="MaxTouchpointsPerStage">Tenant's maximum touchpoints per stage.</param>
public sealed record JourneyLimitsDto(int MaxStagesPerJourney, int MaxTouchpointsPerStage);
