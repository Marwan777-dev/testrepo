namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// A per-stage or per-touchpoint threshold override (tenant-schema table
/// <c>detection_threshold_overrides</c>). The most specific override wins:
/// touchpoint &gt; stage &gt; journey default. Null threshold fields inherit from the parent.
/// </summary>
public sealed class DetectionThresholdOverride
{
    public Guid OverrideId { get; set; }

    /// <summary>Owning detection config (FK → <c>detection_configs.detection_config_id</c> ON DELETE CASCADE).</summary>
    public Guid DetectionConfigId { get; set; }

    /// <summary><c>stage</c> | <c>touchpoint</c> — the level this override applies to.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>
    /// The <c>stage_id</c> or <c>touchpoint_id</c> this override targets, depending on
    /// <see cref="ScopeType"/>. No FK (polymorphic reference) — existence is enforced at
    /// the service layer.
    /// </summary>
    public Guid ScopeId { get; set; }

    /// <summary>Score ≤ this value = pain point; null means "inherit from parent". <c>numeric(5,2)</c>, [0, 100].</summary>
    public decimal? PainThreshold { get; set; }

    /// <summary>Score ≥ this value = happy moment; null means "inherit from parent". <c>numeric(5,2)</c>, [0, 100].</summary>
    public decimal? HappyThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
