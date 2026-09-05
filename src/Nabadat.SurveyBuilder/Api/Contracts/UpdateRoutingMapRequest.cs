namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Body of <c>PUT /api/v1/surveys/{id}/questions/{qid}/routing</c> — the F9 per-question routing map
/// save (contracts/questions.md). Only entries that deviate from the next-in-order default need be
/// present; absent entries fall back to the default (research.md §6). Each value is a target question
/// id, or "<c>__end</c>" (<see cref="RoutingMapView.EndSentinel"/>) for end-of-survey. Sending an
/// empty map clears every override for the question.
/// </summary>
/// <param name="Map">Answer-key → target ("<c>&lt;question_id&gt;</c>" | "<c>__end</c>") override entries.</param>
public sealed record UpdateRoutingMapRequest(IReadOnlyDictionary<string, string> Map);
