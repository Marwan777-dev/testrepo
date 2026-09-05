namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Response body for the US2 approval endpoints (submit / publish / return-to-draft). Carries the
/// survey's new lifecycle <see cref="Status"/> (PascalCase member name, e.g. <c>PendingReview</c>)
/// and the new <see cref="RowVersion"/> ETag; the same value is also returned in the <c>ETag</c>
/// response header. Clients needing the full survey re-read <c>GET /surveys/{id}</c>.
/// </summary>
public sealed record ApprovalActionResult(Guid SurveyId, string Status, int RowVersion);
