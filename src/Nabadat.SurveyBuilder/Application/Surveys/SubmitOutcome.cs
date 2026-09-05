using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <see cref="ApprovalStateMachine.Submit"/> — a Draft submitted for review (FR-15.1).
/// The survey moves to <see cref="SurveyStatus.PendingReview"/>, the reviewers to notify are named
/// by <see cref="NotificationTo"/> (the gating permission, Q7 broadcast), and the survey is locked
/// against edits by <see cref="EditLockOwner"/> (the submitting role, BR-15.1).
/// </summary>
public sealed record SubmitOutcome(SurveyStatus NewStatus, string NotificationTo, string EditLockOwner);
