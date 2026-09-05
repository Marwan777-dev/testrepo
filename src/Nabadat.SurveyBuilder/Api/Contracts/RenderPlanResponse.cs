using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// GET/POST /api/v1/surveys/{id}/render-plan response (contracts/surveys.md) — the dispatch-time seam
/// for M-02/M-04 and the admin diagnostics view. Projects the published <see cref="RenderPlan"/>:
/// sections in FR-10.4 order, each with its ordered items (standalone questions + Questions Set
/// samples), and the sparse routing map (<c>question_id → answer_key → target_question_id | "__end"</c>).
/// </summary>
public sealed record RenderPlanResponse(
    Guid SurveyId,
    LayoutMode Layout,
    IReadOnlyList<RenderPlanSection> SectionsOrder,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> RoutingMap)
{
    /// <summary>Projects the published <see cref="RenderPlan"/> value into the wire contract.</summary>
    public static RenderPlanResponse From(RenderPlan plan) => new(
        plan.SurveyId.Value,
        plan.Layout,
        plan.Sections
            .Select(section => new RenderPlanSection(section.SectionId, section.Items.Select(MapItem).ToList()))
            .ToList(),
        plan.RoutingMap.ToDictionary(
            source => source.Key.ToString(),
            source => (IReadOnlyDictionary<string, string>)source.Value.ToDictionary(
                answer => answer.Key,
                answer => answer.Value.TargetQuestionId?.ToString() ?? "__end")));

    private static RenderPlanItem MapItem(RenderItem item) => item switch
    {
        RenderQuestion question => RenderPlanItem.Question(question.QuestionId),
        RenderSetSample set => RenderPlanItem.Set(set.SetId, set.QuestionIds),
        _ => throw new InvalidOperationException($"Unknown render item '{item.GetType().Name}'."),
    };
}
