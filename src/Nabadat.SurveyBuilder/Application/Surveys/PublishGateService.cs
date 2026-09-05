using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The BR-1.7 (Q9) publish content gate (T070): a survey entering Active from Draft or PendingReview
/// must have ≥1 section AND ≥1 question. Reactivating a Paused survey is NOT gated (Q9). The pure
/// check takes the counts directly; <c>SurveyLifecycleService</c> sources them from
/// <c>ISurveyStore.GetContentCountsAsync</c>.
/// </summary>
public sealed class PublishGateService
{
    public PublishGateResult EnsureContent(SurveyContentCounts counts, SurveyStatus current, SurveyStatus target)
    {
        var gated = target == SurveyStatus.Active
            && (current == SurveyStatus.Draft || current == SurveyStatus.PendingReview);

        if (!gated)
        {
            return PublishGateResult.NotGated();
        }

        var missingSections = counts.SectionsCount == 0;
        var missingQuestions = counts.QuestionsCount == 0;

        return missingSections || missingQuestions
            ? PublishGateResult.Rejected(missingSections, missingQuestions)
            : PublishGateResult.Satisfied();
    }
}
