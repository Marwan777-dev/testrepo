namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Scale question payload (Labels / Stars / Smileys / Slider sub-types — research.md §5). A points
/// scale uses <see cref="PointCount"/> + optional <see cref="Labels"/>; a Slider uses
/// <see cref="SliderLower"/>/<see cref="SliderHigher"/>/<see cref="SliderSteps"/> (steps ≥ 1,
/// <c>scale.slider.steps.min</c>). Which fields are required per sub-type is enforced by
/// <c>QuestionValidator</c>.
/// </summary>
/// <param name="PointCount">Number of scale points (Labels/Stars/Smileys).</param>
/// <param name="Labels">Optional per-point labels.</param>
/// <param name="SliderLower">Slider lower bound.</param>
/// <param name="SliderHigher">Slider upper bound.</param>
/// <param name="SliderSteps">Slider step count (≥ 1).</param>
public sealed record ScalePayload(
    int? PointCount = null,
    IReadOnlyList<string>? Labels = null,
    int? SliderLower = null,
    int? SliderHigher = null,
    int? SliderSteps = null) : QuestionTypePayload;
