using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Domain.Entities;

/// <summary>
/// A survey question — single table with a per-type jsonb payload (tenant-schema table
/// <c>questions</c>, data-model.md §2.4, research.md §5). Common columns live here; the per-type
/// fields live in <see cref="TypePayload"/> (a polymorphic <see cref="QuestionTypePayload"/>). KPI
/// binding is stored flat (<see cref="KpiCode"/>, <see cref="Perspective"/>,
/// <see cref="BoundJourneyOn"/>, <see cref="StageId"/>, <see cref="TouchpointId"/>) — cross-module
/// identifiers with no FK, validated at write time (FR-8.4 / BR-8.2 / BR-8.5).
/// </summary>
public sealed class Question
{
    public Guid Id { get; set; }

    /// <summary>Owning survey (denormalised from the section for fast render-plan reads). No FK across the set.</summary>
    public Guid SurveyId { get; set; }

    public Guid SectionId { get; set; }

    /// <summary>Owning questions-set; null ⇒ standalone (only standalone questions can route, FR-9.5).</summary>
    public Guid? SetId { get; set; }

    public QuestionType Type { get; set; }

    /// <summary>Per-type display mode (required, FR-8.8); <see cref="QuestionSubType.None"/> for variant-less types.</summary>
    public QuestionSubType Subtype { get; set; } = QuestionSubType.None;

    public string Text { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool Required { get; set; }

    /// <summary>FR-8.9 — adds an optional free-text comment box under the question.</summary>
    public bool Comments { get; set; }

    /// <summary>Translatable label above the comment box; default "Comments".</summary>
    public string CommentLabel { get; set; } = "Comments";

    public int CommentMaxLength { get; set; } = 200;

    /// <summary>FR-8.11 — only valid for Input Field Text/Paragraph (enforced by <c>SentimentFlagPolicy</c>).</summary>
    public bool Sentiment { get; set; }

    /// <summary>KPI questions and Matrix KPI-scale mode. M-06 catalogue code; no FK.</summary>
    public string? KpiCode { get; set; }

    public string? Perspective { get; set; }

    /// <summary>Default true for KPI questions; false clears <see cref="StageId"/>/<see cref="TouchpointId"/> (BR-8.2).</summary>
    public bool BoundJourneyOn { get; set; } = true;

    /// <summary>M-16 journey stage; required before <see cref="TouchpointId"/> may be set (FR-8.4). No FK.</summary>
    public Guid? StageId { get; set; }

    /// <summary>M-16 touchpoint; optional, validated by <c>IJourneyReader.IsBindingValidAsync</c>. No FK.</summary>
    public Guid? TouchpointId { get; set; }

    /// <summary>Per-type validated payload (research.md §5) — set by <c>QuestionCommandService</c> on write.</summary>
    public QuestionTypePayload TypePayload { get; set; } = null!;

    /// <summary>Ordering within <c>(section_id, set_id)</c> — contiguous.</summary>
    public int Order { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Monotonic ETag counter (research.md §2).</summary>
    public int RowVersion { get; set; } = 1;

    /// <summary>Bumps the ETag counter — call inside the write transaction on every mutation.</summary>
    public void IncrementRowVersion() => RowVersion++;
}
