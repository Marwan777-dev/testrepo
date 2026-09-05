namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/surveys/{id}/return-to-draft</c> (US2,
/// contracts/approval-workflow.md). <see cref="Remarks"/> are the reviewer's required notes,
/// recorded in the M-17 audit log (FR-15.3). The endpoint rejects a blank value with 400
/// <c>survey.return_to_draft.remarks_required</c>.
/// </summary>
public sealed record ReturnToDraftRequest(string Remarks);
