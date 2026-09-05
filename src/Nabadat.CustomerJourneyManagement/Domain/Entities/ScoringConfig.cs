namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Tenant-level strategic scoring parameters (tenant-schema table <c>scoring_configs</c>,
/// <b>exactly one row per tenant</b> — SRS §4.2.9 / §11.7, Q11 RESOLVED: per-tenant, NOT per-journey).
/// All journeys in the tenant share these parameters, keeping scoring methodology consistent and
/// cross-journey comparable. Owned by M-16, read by M-06 (via <c>IScoringConfigStore</c>), and edited
/// from the Platform Settings → Customer Journey surface (feature 003). The singleton is enforced by a
/// unique index on a constant expression (<c>((true))</c>); there is no <c>journey_id</c> and no
/// <c>tenant_id</c> (the schema boundary is the tenant scope, AD-02).
/// </summary>
public sealed class ScoringConfig
{
    public Guid ScoringConfigId { get; set; }

    /// <summary>α blend weight, <c>numeric(4,3)</c> ∈ [0.000, 1.000]. β is derived as <c>1 − α</c> (not stored).</summary>
    public decimal Alpha { get; set; }

    /// <summary>Moment-of-Truth weight multiplier, <c>numeric(3,1)</c> ∈ [1.0, 2.0].</summary>
    public decimal MotMultiplier { get; set; }

    /// <summary>Hard minimum response count (≥ 1); below it a touchpoint is excluded from scoring.</summary>
    public int NFloor { get; set; }

    /// <summary>Percentile k for the low-sample flag threshold, ∈ [1, 49].</summary>
    public int FlagPercentile { get; set; }

    /// <summary>Rolling response window in days (≥ 7).</summary>
    public int RollingWindowDays { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>M-10 <c>user_id</c> of the last editor (P-01 only can edit).</summary>
    public Guid UpdatedBy { get; set; }
}
