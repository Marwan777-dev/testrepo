using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// T113 [US2] — the pure state-transition policy behind the P-03 → Draft → PendingReview → Active
/// approval loop (FR-15.1/.3/.5, BR-15.2). It decides the <i>target</i> status and the role/grant
/// gate for each approval action; it performs no I/O and reads no clock. Enforcing that the survey
/// is in the right <i>current</i> status for a transition is the orchestrator's job
/// (<c>ApprovalWorkflowService</c>, T118, via the US1 <c>StatusTransitionPolicy</c>); this type
/// answers "given the current status and actor, what is the outcome?".
/// </summary>
public sealed class ApprovalStateMachine
{
    private const string ProgramManagerRole = "P-01"; // reviewer / publisher
    private const string SurveyAdminRole = "P-03";     // author / submitter
    private const string PublishOwnSurveysGrant = "PublishOwnSurveys";
    private const string ReviewPermission = "survey.publish";

    /// <summary>
    /// Submit a Draft for review: the survey moves to <see cref="SurveyStatus.PendingReview"/>, the
    /// reviewers holding <c>survey.publish</c> are the notification target (Q7), and the survey is
    /// edit-locked to the submitting role (BR-15.1).
    /// </summary>
    public SubmitOutcome Submit(SurveyStatus current, string actorRole)
        => new(SurveyStatus.PendingReview, NotificationTo: ReviewPermission, EditLockOwner: actorRole);

    /// <summary>
    /// Decide whether <paramref name="actorRole"/> may publish. P-01 (reviewer) always may; a P-03
    /// may only with the <c>PublishOwnSurveys</c> grant AND on a survey they personally authored
    /// (<paramref name="ownerId"/> == <paramref name="actorId"/>). Otherwise the request is
    /// <see cref="PublishDecision.Forbidden"/> and the status is left unchanged.
    /// </summary>
    public PublishOutcome Publish(SurveyStatus current, string actorRole, string? grant, Guid? ownerId, Guid actorId)
    {
        var permitted = actorRole == ProgramManagerRole
            || (actorRole == SurveyAdminRole
                && grant == PublishOwnSurveysGrant
                && ownerId == actorId);

        return permitted
            ? new PublishOutcome(PublishDecision.Published, SurveyStatus.Active)
            : new PublishOutcome(PublishDecision.Forbidden, current);
    }

    /// <summary>
    /// Return a PendingReview survey to its author (FR-15.3): the survey moves to
    /// <see cref="SurveyStatus.Draft"/> and reviewer <paramref name="remarks"/> are recorded for the
    /// audit log when present.
    /// </summary>
    public ReturnToDraftOutcome ReturnToDraft(SurveyStatus current, string actorRole, string remarks)
        => new(SurveyStatus.Draft, RemarksPersisted: !string.IsNullOrWhiteSpace(remarks));
}
