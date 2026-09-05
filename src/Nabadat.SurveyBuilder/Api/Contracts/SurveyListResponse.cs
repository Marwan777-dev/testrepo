namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>F1 Survey Library page (contracts/surveys.md GET /surveys) — cursor pagination (API-04).</summary>
public sealed record SurveyListResponse(
    IReadOnlyList<SurveyListItem> Items,
    string? NextPageToken,
    int TotalCount);
