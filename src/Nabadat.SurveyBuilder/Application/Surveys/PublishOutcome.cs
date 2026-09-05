using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <see cref="ApprovalStateMachine.Publish"/>. When <see cref="Decision"/> is
/// <see cref="PublishDecision.Published"/> the <see cref="NewStatus"/> is <c>Active</c>; when it is
/// <see cref="PublishDecision.Forbidden"/> the status is unchanged (the caller's current status).
/// </summary>
public sealed record PublishOutcome(PublishDecision Decision, SurveyStatus NewStatus);
