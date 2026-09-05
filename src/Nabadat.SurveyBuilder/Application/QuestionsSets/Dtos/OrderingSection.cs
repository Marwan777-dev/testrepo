namespace Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

/// <summary>
/// A section reduced to the question ids eligible to determine its low-response ordering key
/// (T141, FR-10.4). The section's key is the lowest response count among <see cref="QuestionIds"/>.
/// </summary>
public sealed record OrderingSection(Guid SectionId, IReadOnlyList<Guid> QuestionIds);
