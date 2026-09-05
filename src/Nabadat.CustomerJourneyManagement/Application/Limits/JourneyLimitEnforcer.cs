using Microsoft.Extensions.Logging;

namespace Nabadat.CustomerJourneyManagement.Application.Limits;

/// <summary>
/// The concrete <see cref="IJourneyLimitProvider"/> (T027 / US-1). Resolves the per-tenant journey
/// limits by calling M-11's tenant service (<see cref="IM11TenantService.GetJourneyLimitsAsync"/>)
/// once per request — no cross-request cache, consistent with AD-03 (limits are request-scoped only).
/// <para>
/// <b>Fallback:</b> if M-11 is unavailable (network failure or an open circuit-breaker) the call
/// throws; this enforcer catches it, logs a warning, and returns the platform defaults
/// (<see cref="DefaultMaxStagesPerJourney"/> / <see cref="DefaultMaxTouchpointsPerStage"/>) so the
/// journey operation proceeds. Rationale (research §9): a limit-check outage is an operational
/// concern, not a hard blocker — it must never stop a tenant from editing a journey. Caller
/// cancellation (<see cref="OperationCanceledException"/>) is not an M-11 outage and is rethrown
/// rather than masked as a fallback.
/// </para>
/// </summary>
public sealed class JourneyLimitEnforcer : IJourneyLimitProvider
{
    /// <summary>Platform default applied when M-11 cannot supply a tenant value: 20 stages per journey.</summary>
    public const int DefaultMaxStagesPerJourney = 20;

    /// <summary>Platform default applied when M-11 cannot supply a tenant value: 30 touchpoints per stage.</summary>
    public const int DefaultMaxTouchpointsPerStage = 30;

    private static readonly JourneyLimits PlatformDefaults =
        new(DefaultMaxStagesPerJourney, DefaultMaxTouchpointsPerStage);

    private readonly IM11TenantService _tenants;
    private readonly ILogger<JourneyLimitEnforcer> _logger;

    public JourneyLimitEnforcer(IM11TenantService tenants, ILogger<JourneyLimitEnforcer> logger)
    {
        _tenants = tenants;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<JourneyLimits> GetLimitsAsync(CancellationToken ct = default)
    {
        try
        {
            var limits = await _tenants.GetJourneyLimitsAsync(ct);
            return new JourneyLimits(limits.MaxStagesPerJourney, limits.MaxTouchpointsPerStage);
        }
        catch (OperationCanceledException)
        {
            // Caller cancelled the request — not an M-11 outage. Propagate rather than masking
            // the cancellation as a successful fallback to defaults.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "M-11 journey-limit lookup failed; falling back to platform defaults " +
                "({MaxStages} stages / {MaxTouchpoints} touchpoints per stage).",
                PlatformDefaults.MaxStagesPerJourney,
                PlatformDefaults.MaxTouchpointsPerStage);

            return PlatformDefaults;
        }
    }
}
