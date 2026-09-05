namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// Command for the US2 non-destructive return-to-draft action (PendingReview → Draft by a reviewer),
/// orchestrated by <c>ApprovalWorkflowService.ReturnToDraftAsync</c> (T118). Distinct from the
/// destructive Active/Paused → Draft <c>ReturnToDraftCommand</c> (BR-1.6, which purges responses via
/// <c>SurveyLifecycleService</c>). <see cref="Remarks"/> are the required reviewer notes (FR-15.3).
/// </summary>
public sealed record ReturnForRevisionCommand(Guid SurveyId, Guid ActorId, string ActorRole, string Remarks, Guid CorrelationId);
