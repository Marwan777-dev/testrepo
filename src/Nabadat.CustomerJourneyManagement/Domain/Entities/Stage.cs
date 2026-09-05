namespace Nabadat.CustomerJourneyManagement.Domain.Entities;

/// <summary>
/// Ordered phase within a journey (tenant-schema table <c>stages</c>). Every touchpoint
/// belongs to a stage. <see cref="SequenceNumber"/> is 1-based and unique within the journey.
/// </summary>
public sealed class Stage
{
    public Guid StageId { get; set; }

    /// <summary>Parent journey (FK → <c>journeys.journey_id</c> ON DELETE CASCADE).</summary>
    public Guid JourneyId { get; set; }

    /// <summary>1-based ordering position; unique within the journey.</summary>
    public int SequenceNumber { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>What the customer is trying to achieve in this stage.</summary>
    public string? CustomerGoal { get; set; }

    /// <summary>e.g. <c>excited</c>, <c>anxious</c>, <c>frustrated</c>, <c>satisfied</c>.</summary>
    public string? ExpectedEmotion { get; set; }

    /// <summary>Human-readable estimate, e.g. <c>2–5 minutes</c>.</summary>
    public string? DurationHint { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
