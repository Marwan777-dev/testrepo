using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// T114 [US2] — the PendingReview edit-lock rule (BR-15.1, contract § "Edit-lock behaviour on
/// PendingReview"). While a survey is PendingReview its <b>submitter</b> (a P-03) is edit-locked; the
/// <b>reviewer</b> (P-01) may still edit before publishing. In any other status the lock does not
/// apply (team-owned Draft editing, Q8). Enforced by the <c>EditLockFilter</c> on write endpoints.
/// </summary>
public sealed class EditLockPolicy
{
    private const string SurveyAdminRole = "P-03";
    private const string EditLockedCode = "survey.edit_locked_by_pending_review";

    /// <summary>
    /// Evaluate whether <paramref name="callerRole"/> / <paramref name="callerUserId"/> may edit the
    /// given <paramref name="survey"/>. Locked only when the caller is the P-03 submitter of a
    /// PendingReview survey; permitted otherwise.
    /// </summary>
    public EditLockResult Evaluate(string callerRole, Guid callerUserId, EditLockState survey)
    {
        var locked = callerRole == SurveyAdminRole
            && survey.Status == SurveyStatus.PendingReview
            && survey.SubmittedByUserId == callerUserId;

        return locked
            ? new EditLockResult(CanEdit: false, Reason: EditLockedCode)
            : new EditLockResult(CanEdit: true, Reason: null);
    }
}
