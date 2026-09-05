namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>F6 template list (contracts/templates.md GET /templates) — customized-first, cursor pagination (API-04).</summary>
public sealed record TemplateListResponse(
    IReadOnlyList<TemplateListItem> Items,
    string? NextPageToken,
    int TotalCount);
