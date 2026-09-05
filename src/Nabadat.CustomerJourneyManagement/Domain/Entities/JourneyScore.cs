namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// The latest computed score snapshot for a journey (tenant-schema table
/// <c>journey_scores</c>, one row per journey). Upserted on every call to
/// <c>IJourneyScoreProvider.GetScoresAsync()</c> via
/// <c>INSERT ... ON CONFLICT (journey_id) DO UPDATE</c>, inside the same transaction as
/// the <c>journey.score.updated</c> M-17 event.
/// </summary>
public sealed class JourneyScore
{
    public Guid JourneyScoreId { get; set; }

    /// <summary>Owning journey (FK → <c>journeys.journey_id</c> ON DELETE CASCADE, UNIQUE).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>UTC timestamp when the scores were last computed.</summary>
    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>
    /// Composite journey score (DB column <c>journey_score</c>, <c>numeric(5,2)</c>);
    /// null when the journey has no measured touchpoints.
    /// </summary>
    public decimal? CompositeScore { get; set; }

    /// <summary>Per-stage scores as opaque JSON: <c>[{ stageId, score, measuredTouchpointCount }]</c>.</summary>
    public string? StageScores { get; set; }

    /// <summary>Per-touchpoint scores as opaque JSON: <c>[{ touchpointId, score, kpiScores }]</c>.</summary>
    public string? TouchpointScores { get; set; }
}
