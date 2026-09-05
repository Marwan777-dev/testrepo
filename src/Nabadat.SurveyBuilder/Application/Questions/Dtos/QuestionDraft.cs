using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions.Dtos;

/// <summary>
/// The F8 question authoring input validated by <c>QuestionValidator</c> (T075). Common fields plus
/// the per-type discriminators the validator needs (<see cref="SubType"/>, <see cref="SliderSteps"/>);
/// the full per-type payload is assembled by <c>QuestionCommandService</c> (T079).
/// </summary>
public sealed class QuestionDraft
{
    public string? Text { get; init; }

    public QuestionType Type { get; init; }

    public QuestionSubType? SubType { get; init; }

    /// <summary>Slider step count (Scale/Slider only); must be ≥ 1 (<c>scale.slider.steps.min</c>).</summary>
    public int? SliderSteps { get; init; }
}
