using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The minimal survey state <see cref="EditLockPolicy"/> needs to decide the PendingReview edit lock
/// (BR-15.1): the current <see cref="Status"/> and the user who submitted it for review
/// (<see cref="SubmittedByUserId"/>, null when never submitted).
/// </summary>
public sealed record EditLockState(SurveyStatus Status, Guid? SubmittedByUserId);
