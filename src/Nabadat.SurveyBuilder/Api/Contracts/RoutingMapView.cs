using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Response body for the F9 per-question routing endpoints (contracts/questions.md GET/PUT
/// <c>/questions/{qid}/routing</c>). Carries the sparse override map — one entry per answer key whose
/// target deviates from the next-in-order default; missing entries fall back to the default, which
/// the client rehydrates locally (research.md §6). A target is a question id string, or the
/// <see cref="EndSentinel"/> for end-of-survey.
/// </summary>
/// <param name="Map">Answer-key → target ("<c>&lt;question_id&gt;</c>" | "<c>__end</c>") override entries.</param>
/// <param name="HasRouting">True when at least one override exists (drives the "Routing set" badge).</param>
public sealed record RoutingMapView(IReadOnlyDictionary<string, string> Map, bool HasRouting)
{
    /// <summary>Reserved target value meaning "end the survey" (null <c>target_question_id</c>).</summary>
    public const string EndSentinel = "__end";

    /// <summary>Projects a question's override rows into the wire map.</summary>
    public static RoutingMapView From(IReadOnlyList<RoutingMap> overrides)
    {
        var map = overrides.ToDictionary(
            r => r.AnswerKey,
            r => r.TargetQuestionId?.ToString() ?? EndSentinel);
        return new RoutingMapView(map, map.Count > 0);
    }
}
