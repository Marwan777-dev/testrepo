using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Keeps <c>surveys.survey_type</c> in sync with <c>surveys.bound_journey_id</c> (BR-3.3, T068): a
/// bound journey ⇒ <see cref="SurveyType.Transactional"/>, cleared ⇒
/// <see cref="SurveyType.SeasonalRelational"/>. Pure.
/// </summary>
public sealed class SurveyTypeSyncService
{
    public void OnBoundJourneyChanged(Survey survey, Guid? journeyId)
    {
        survey.BoundJourneyId = journeyId;
        survey.SurveyType = journeyId is null ? SurveyType.SeasonalRelational : SurveyType.Transactional;
    }
}
