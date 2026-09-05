namespace Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

/// <summary>
/// A Questions Set reduced to its member question ids, for low-response candidate selection
/// (T141, FR-10.4). <c>PickCandidates</c> returns the least-answered members up to the set's count.
/// </summary>
public sealed record OrderingSet(Guid SetId, IReadOnlyList<Guid> QuestionIds);
