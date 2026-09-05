using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// Application-layer status-transition gate (T069): delegates the authoritative matrix to
/// <see cref="SurveyStatusTransitions.AllowedTransitions"/> (BR-1.4) and additionally denies
/// <c>Draft → Active</c> while an unpublished pending-review version is outstanding (§3.15 lock).
/// </summary>
public sealed class StatusTransitionPolicy
{
    public bool Allowed(
        SurveyStatus current,
        SurveyStatus next,
        string actorRole,
        bool isDestructive = false,
        bool hasUnpublishedPendingReview = false)
    {
        if (current == SurveyStatus.Draft && next == SurveyStatus.Active && hasUnpublishedPendingReview)
        {
            return false;
        }

        return SurveyStatusTransitions.AllowedTransitions(current, next, actorRole, isDestructive);
    }
}
