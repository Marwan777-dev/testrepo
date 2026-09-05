namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A sparse per-answer routing override (tenant-schema table <c>routing_maps</c>,
/// data-model.md §2.5, F9). One row per answer whose next target deviates from the default
/// (next-in-order); a missing row means "use the default" (research.md §6). This table has no
/// <c>row_version</c> (data-model.md §2.5).
/// <para>Source and target must both be standalone (<c>set_id IS NULL</c>) and share the same
/// <see cref="SurveyId"/> — cross-row invariants enforced at the App layer by
/// <c>RoutingEligibilityService</c> (FR-9.5), not by a DB constraint.</para>
/// </summary>
public sealed class RoutingMap
{
    public Guid Id { get; set; }

    /// <summary>Owning survey (denormalised for cascade-on-survey-delete and survey-scoped invalidation). ON DELETE CASCADE.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>The eligible standalone question this route branches from. ON DELETE CASCADE.</summary>
    public Guid SourceQuestionId { get; set; }

    /// <summary>Per-type answer identifier (Scale point index, YesNo "yes"/"no", SingleSelect option id, KPI score bucket).</summary>
    public string AnswerKey { get; set; } = string.Empty;

    /// <summary>Target question; null ⇒ <c>__end</c> (end of survey). ON DELETE SET NULL (FR-2.7 reset-to-default).</summary>
    public Guid? TargetQuestionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
