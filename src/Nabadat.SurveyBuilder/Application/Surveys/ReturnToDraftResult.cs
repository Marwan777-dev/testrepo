namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Outcome of <c>DestructiveReturnToDraftService.ReturnToDraftAsync</c> (T072): whether responses
/// were purged (destructive Active/Paused → Draft) and how many, plus the new ETag row-version.
/// </summary>
public sealed record ReturnToDraftResult(bool Purged, int PurgedResponseCount, int RowVersion);
