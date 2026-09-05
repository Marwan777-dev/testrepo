namespace Nabadat.SurveyBuilder.Domain.ValueObjects;

/// <summary>
/// The authoritative Survey Status Transition Matrix (spec.md → "Survey Status Transition Matrix
/// (authoritative)", BR-1.4) as pure logic. Companion to the <see cref="SurveyStatus"/> enum
/// (kept in its own file per the one-type-per-file convention, mirroring
/// <c>IdentityProviderTypeExtensions</c>).
/// </summary>
public static class SurveyStatusTransitions
{
    /// <summary>CX Manager persona — may perform every transition in the matrix.</summary>
    public const string ManagerRole = "P-01";

    /// <summary>
    /// Survey Author persona — may Submit-for-review (own draft) and Publish (own draft, with the
    /// "Publish own surveys" grant). Per-instance ownership + the grant are authorization concerns
    /// enforced by the service layer (<c>IPermissionChecker</c>); this matrix only encodes which
    /// role is *structurally* permitted the transition.
    /// </summary>
    public const string AuthorRole = "P-03";

    /// <summary>
    /// Returns whether transitioning <paramref name="from"/> → <paramref name="to"/> is permitted
    /// for <paramref name="actorRole"/> per the matrix.
    /// <para><paramref name="isDestructive"/> is the BR-1.6 destructive marker: the only destructive
    /// transitions are <c>Active → Draft</c> and <c>Paused → Draft</c> (Return-to-Draft to edit,
    /// which purges responses). The caller MUST pass <c>true</c> for those (the confirm path) and
    /// <c>false</c> for every other transition; a mismatch returns <c>false</c> so a destructive
    /// edit can never be triggered without acknowledgement, nor a benign transition flagged as
    /// destructive.</para>
    /// </summary>
    public static bool AllowedTransitions(SurveyStatus from, SurveyStatus to, string actorRole, bool isDestructive)
    {
        var transitionIsDestructive =
            to == SurveyStatus.Draft && (from == SurveyStatus.Active || from == SurveyStatus.Paused);
        if (transitionIsDestructive != isDestructive)
        {
            return false;
        }

        return (from, to) switch
        {
            (SurveyStatus.Draft, SurveyStatus.PendingReview)  => actorRole is ManagerRole or AuthorRole,
            (SurveyStatus.Draft, SurveyStatus.Active)         => actorRole is ManagerRole or AuthorRole,
            (SurveyStatus.PendingReview, SurveyStatus.Active) => actorRole is ManagerRole or AuthorRole,
            (SurveyStatus.PendingReview, SurveyStatus.Draft)  => actorRole == ManagerRole,
            (SurveyStatus.Active, SurveyStatus.Draft)         => actorRole == ManagerRole, // destructive (BR-1.6)
            (SurveyStatus.Active, SurveyStatus.Paused)        => actorRole == ManagerRole,
            (SurveyStatus.Paused, SurveyStatus.Active)        => actorRole == ManagerRole,
            (SurveyStatus.Paused, SurveyStatus.Draft)         => actorRole == ManagerRole, // destructive (BR-1.6)
            (SurveyStatus.Draft, SurveyStatus.Archived)       => actorRole == ManagerRole,
            (SurveyStatus.Active, SurveyStatus.Archived)      => actorRole == ManagerRole,
            (SurveyStatus.Paused, SurveyStatus.Archived)      => actorRole == ManagerRole,
            (SurveyStatus.Archived, SurveyStatus.Draft)       => actorRole == ManagerRole, // Unarchive (BR-1.3)
            _ => false,
        };
    }
}
