namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// Command for the US2 submit-for-review action (Draft → PendingReview), orchestrated by
/// <c>ApprovalWorkflowService.SubmitAsync</c> (T118). The actor is the submitting author; the
/// correlation id ties the audit event and M-09 broadcast together.
/// </summary>
public sealed record SubmitForReviewCommand(Guid SurveyId, Guid ActorId, string ActorRole, Guid CorrelationId);
