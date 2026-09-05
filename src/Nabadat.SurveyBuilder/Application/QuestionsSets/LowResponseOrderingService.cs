using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

namespace Nabadat.SurveyBuilder.Application.QuestionsSets;

/// <summary>
/// FR-10.4 low-response ordering (research.md §7). A pure in-memory function over a per-survey
/// response-count projection (<c>question_id → count</c>). The cascade is Set → Section → Survey:
/// within a set, pick the least-answered members; a section's ordering key is the lowest response
/// count among its eligible questions; sections are ordered ascending by that key (least-answered
/// first). A question absent from the projection counts as 0 (never answered ⇒ highest priority).
/// </summary>
public sealed class LowResponseOrderingService
{
    /// <summary>Section ids ordered by ascending lowest-response question (least-answered section first).</summary>
    public IReadOnlyList<Guid> OrderSections(
        IReadOnlyList<OrderingSection> sections,
        IReadOnlyDictionary<Guid, long> responseCounts)
    {
        return sections
            .OrderBy(section => SectionKey(section, responseCounts))
            .Select(section => section.SectionId)
            .ToList();
    }

    /// <summary>The <paramref name="count"/> least-answered member questions of a set, ascending.</summary>
    public IReadOnlyList<Guid> PickCandidates(
        OrderingSet set,
        int count,
        IReadOnlyDictionary<Guid, long> responseCounts)
    {
        return set.QuestionIds
            .OrderBy(questionId => CountOf(responseCounts, questionId))
            .Take(count)
            .ToList();
    }

    private static long SectionKey(OrderingSection section, IReadOnlyDictionary<Guid, long> responseCounts) =>
        section.QuestionIds.Count == 0
            ? long.MaxValue // an empty section sorts last — it has no low-response signal
            : section.QuestionIds.Min(questionId => CountOf(responseCounts, questionId));

    private static long CountOf(IReadOnlyDictionary<Guid, long> responseCounts, Guid questionId) =>
        responseCounts.TryGetValue(questionId, out var count) ? count : 0L;
}
