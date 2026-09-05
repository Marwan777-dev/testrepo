namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Journey-level pain/happy detection thresholds (tenant-schema table
/// <c>detection_configs</c>, one row per journey). The invariant
/// <c>pain_threshold &lt; happy_threshold</c> is enforced at the service layer; the
/// neutral band between the two thresholds is valid.
/// </summary>
public sealed class DetectionConfig
{
    public Guid DetectionConfigId { get; set; }

    /// <summary>Owning journey (FK → <c>journeys.journey_id</c> ON DELETE CASCADE, UNIQUE).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>Score ≤ this value = pain point. <c>numeric(5,2)</c>, in range [0, 100].</summary>
    public decimal PainThreshold { get; set; }

    /// <summary>Score ≥ this value = happy moment. <c>numeric(5,2)</c>, in range [0, 100].</summary>
    public decimal HappyThreshold { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
