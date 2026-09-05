namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The section / question counts for a survey, sourced from <c>ISurveyStore.GetContentCountsAsync</c>
/// and consumed by the BR-1.7 publish gate (<c>PublishGateService</c>, T070). <see cref="QuestionsCount"/>
/// is the total across all sections (standalone + set members).
/// </summary>
public sealed record SurveyContentCounts(int SectionsCount, int QuestionsCount);
