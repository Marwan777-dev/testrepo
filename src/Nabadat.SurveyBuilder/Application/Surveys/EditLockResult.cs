namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <see cref="EditLockPolicy.Evaluate"/>: whether the caller may edit the survey, and — when
/// not — the API-05 error <see cref="Reason"/> code the write endpoint returns (403).
/// </summary>
public sealed record EditLockResult(bool CanEdit, string? Reason);
