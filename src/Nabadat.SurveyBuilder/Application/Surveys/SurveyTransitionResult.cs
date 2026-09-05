using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Outcome of <c>SurveyLifecycleService.ChangeStatusAsync</c> (T073): the survey's status after the
/// transition and the new ETag row-version.
/// </summary>
public sealed record SurveyTransitionResult(SurveyStatus Status, int RowVersion);
