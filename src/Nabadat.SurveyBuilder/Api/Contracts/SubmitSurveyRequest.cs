namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/surveys/{id}/submit</c> (US2, contracts/approval-workflow.md).
/// Submit-for-review carries no payload today — the survey id is the route and the actor comes from
/// the session; this record exists for API symmetry and forward-compatibility (a future submit note
/// would land here). The endpoint binds it as optional and reads nothing from it.
/// </summary>
public sealed record SubmitSurveyRequest;
