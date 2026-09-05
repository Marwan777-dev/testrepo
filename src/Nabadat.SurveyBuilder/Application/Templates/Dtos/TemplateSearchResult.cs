using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Templates.Dtos;

/// <summary>One page of template search results (contracts/templates.md GET /templates), customized-first.</summary>
public sealed record TemplateSearchResult(
    IReadOnlyList<Template> Items,
    string? NextPageToken,
    int TotalCount);
