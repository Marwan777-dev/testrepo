using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>One page of F1 Survey Library results (contracts/surveys.md GET /surveys).</summary>
public sealed record SurveySearchResult(IReadOnlyList<Survey> Items, string? NextPageToken, int TotalCount);
