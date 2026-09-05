namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// Whether a survey is tied to a customer journey (tenant-schema column <c>surveys.survey_type</c>,
/// data-model.md §2.1). Kept in sync with <c>surveys.bound_journey_id</c> by
/// <c>SurveyTypeSyncService</c> (BR-3.3): a bound journey ⇒ <see cref="Transactional"/>, no bound
/// journey ⇒ <see cref="SeasonalRelational"/>. Wire/DB form is the PascalCase member name.
/// </summary>
public enum SurveyType
{
    /// <summary>Bound to a journey — feedback tied to a specific interaction (BR-3.3).</summary>
    Transactional,

    /// <summary>Not bound to a journey — periodic / relationship surveys.</summary>
    SeasonalRelational,
}
