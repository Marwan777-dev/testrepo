namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 → M-06 (and feature 003's Settings → Customer Journey surface).
/// Exposes the <b>tenant-level</b> strategic scoring configuration (SRS §4.2.9 / §11.7, Q11 RESOLVED:
/// one <c>scoring_configs</c> row per tenant, NOT per-journey). M-06 reads it once per computation
/// cycle; the Platform Settings page writes it through <see cref="UpdateAsync"/>. Consumers MUST NOT
/// read M-16 tables directly.
/// </summary>
public interface IScoringConfigStore
{
    /// <summary>
    /// Returns the tenant's scoring parameters. On a fresh tenant with no saved row this returns the
    /// seeded defaults (α=0.500, MOT=1.5, n_floor=100, flag_percentile=25, rolling_window_days=30).
    /// </summary>
    Task<ScoringConfigDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates and upserts the tenant's scoring parameters, publishing
    /// <c>journey.scoring_config.updated</c> in the same transaction (FR-015). On a validation failure
    /// no row is written and the result carries the failing code.
    /// </summary>
    Task<ScoringConfigUpdateResult> UpdateAsync(ScoringConfigUpdate update, ScoringConfigActor actor, CancellationToken ct = default);
}

/// <summary>The tenant's scoring parameters as seen by consumers. <see cref="Beta"/> is derived (<c>1 − α</c>).</summary>
public sealed record ScoringConfigDto(
    decimal Alpha,
    decimal Beta,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

/// <summary>The five tenant scoring parameters to persist (β is derived, never supplied).</summary>
public sealed record ScoringConfigUpdate(
    decimal Alpha,
    decimal MotMultiplier,
    int NFloor,
    int FlagPercentile,
    int RollingWindowDays);

/// <summary>The acting principal for an audited scoring-config update.</summary>
public sealed record ScoringConfigActor(Guid UserId, string Persona, Guid CorrelationId);

/// <summary>Outcome of <see cref="IScoringConfigStore.UpdateAsync"/>: the persisted config, or a failure code.</summary>
public sealed record ScoringConfigUpdateResult(bool IsSuccess, ScoringConfigDto? Config, string? ErrorCode, string? ErrorMessage)
{
    public static ScoringConfigUpdateResult Success(ScoringConfigDto config) => new(true, config, null, null);

    public static ScoringConfigUpdateResult Failure(string code, string message) => new(false, null, code, message);
}
