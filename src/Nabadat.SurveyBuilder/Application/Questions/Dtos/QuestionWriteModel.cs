using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions.Dtos;

/// <summary>
/// Full question write input for <c>QuestionCommandService</c> (T079) — the common fields, the
/// per-type <see cref="Payload"/>, and the optional KPI <see cref="Binding"/>. Validated by
/// <c>QuestionValidator</c> (derived <see cref="QuestionDraft"/>) + <c>KpiBindingValidator</c>.
/// </summary>
public sealed class QuestionWriteModel
{
    public Guid SurveyId { get; init; }

    public Guid SectionId { get; init; }

    public Guid? SetId { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Description { get; init; }

    public QuestionType Type { get; init; }

    public QuestionSubType SubType { get; init; } = QuestionSubType.None;

    public int? SliderSteps { get; init; }

    public bool Required { get; init; }

    public bool ShowComments { get; init; }

    public bool Sentiment { get; init; }

    public int Order { get; init; }

    /// <summary>KPI binding (KPI questions + Matrix KPI-scale mode); null otherwise.</summary>
    public KpiBinding? Binding { get; init; }

    /// <summary>Per-type payload (research.md §5).</summary>
    public QuestionTypePayload Payload { get; init; } = null!;

    /// <summary>The subset needed by <c>QuestionValidator</c>.</summary>
    public QuestionDraft ToDraft() => new()
    {
        Text = Text,
        Type = Type,
        SubType = SubType,
        SliderSteps = SliderSteps,
    };
}
