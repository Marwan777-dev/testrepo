using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Result of <see cref="ApprovalStateMachine.ReturnToDraft"/> — a reviewer sends a PendingReview
/// survey back to its author (FR-15.3). The survey moves to <see cref="SurveyStatus.Draft"/> and
/// <see cref="RemarksPersisted"/> reports whether reviewer remarks were recorded for the audit log.
/// </summary>
public sealed record ReturnToDraftOutcome(SurveyStatus NewStatus, bool RemarksPersisted);
