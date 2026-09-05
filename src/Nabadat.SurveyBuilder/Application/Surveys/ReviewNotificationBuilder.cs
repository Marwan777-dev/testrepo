using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// T116 [US2] — builds the review-notification broadcast fired when a Draft is submitted (FR-15.2,
/// Q7). It targets every tenant user holding <c>survey.publish</c> (the reviewers, P-01 by default)
/// and deep-links them to the survey's F3 Settings screen, rendered from the
/// <c>survey.submitted_for_review</c> template. The orchestrator (T118) passes the result to
/// <see cref="INotificationDispatcher"/> (M-09) for the actual fan-out.
/// </summary>
public sealed class ReviewNotificationBuilder
{
    private const string TenantScope = "tenant";
    private const string ReviewPermission = "survey.publish";
    private const string SubmittedForReviewTemplate = "survey.submitted_for_review";

    /// <summary>
    /// Build the broadcast for a survey just submitted for review by <paramref name="submitterUserId"/>.
    /// </summary>
    public NotificationBroadcast Build(SurveyId surveyId, Guid submitterUserId)
        => new(
            Scope: TenantScope,
            Permission: ReviewPermission,
            DeepLink: $"/surveys/{surveyId.Value}",
            Template: SubmittedForReviewTemplate);
}
