using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// T115 [US2] — answers "may this actor publish this survey?" (FR-15.5, BR-15.2). A reviewer (P-01)
/// is always authorized. A P-03 is authorized only when they hold the <c>PublishOwnSurveys</c> grant
/// (checked via <see cref="IPermissionChecker"/>) AND they personally authored the survey
/// (<c>owner_user_id == caller</c>) — otherwise they must submit for review first. Any other actor is
/// refused with <c>survey.publish.forbidden</c>.
/// </summary>
public sealed class PublishAuthorizationService
{
    private const string ProgramManagerRole = "P-01";
    private const string SurveyAdminRole = "P-03";
    private const string PublishOwnSurveysGrant = "PublishOwnSurveys";
    private const string ForbiddenCode = "survey.publish.forbidden";

    private readonly IPermissionChecker _permissions;

    public PublishAuthorizationService(IPermissionChecker permissions) => _permissions = permissions;

    /// <summary>Authorize (or refuse) <paramref name="actor"/> publishing <paramref name="survey"/>.</summary>
    public async Task<AuthorizationResult> AuthorizeAsync(PublishActor actor, SurveyApprovalInfo survey, CancellationToken ct)
    {
        // Reviewers may always publish — no grant lookup needed.
        if (actor.Role == ProgramManagerRole)
        {
            return new AuthorizationResult(IsAuthorized: true, DenialCode: null);
        }

        // A P-03 may self-publish only their own survey, and only with the grant.
        if (actor.Role == SurveyAdminRole && actor.UserId == survey.OwnerUserId)
        {
            var hasGrant = await _permissions.HasGrantAsync(actor.UserId, PublishOwnSurveysGrant, ct);
            if (hasGrant)
            {
                return new AuthorizationResult(IsAuthorized: true, DenialCode: null);
            }
        }

        return new AuthorizationResult(IsAuthorized: false, DenialCode: ForbiddenCode);
    }
}
