namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Request body for <c>POST /api/v1/surveys/{id}/publish</c> (US2, contracts/approval-workflow.md).
/// <see cref="Remarks"/> is an optional note recorded in the M-17 audit log alongside the
/// <c>survey.published</c> event.
/// </summary>
public sealed record PublishSurveyRequest(string? Remarks);
