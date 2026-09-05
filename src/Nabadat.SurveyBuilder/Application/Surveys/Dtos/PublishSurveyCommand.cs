namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// Command for the US2 publish action (Draft → Active by a reviewer, or PendingReview → Active by a
/// reviewer or a grant-holding author), orchestrated by <c>ApprovalWorkflowService.PublishAsync</c>
/// (T118). <see cref="Remarks"/> is an optional note recorded in the audit log.
/// </summary>
public sealed record PublishSurveyCommand(Guid SurveyId, Guid ActorId, string ActorRole, string? Remarks, Guid CorrelationId);
