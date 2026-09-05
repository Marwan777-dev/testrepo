using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates.Dtos;

/// <summary>
/// F6 template-picker / Templates-tab query (contracts/templates.md GET /templates). <see cref="Q"/>
/// matches name or tag (FR-6.2); <see cref="Class"/>/<see cref="Sector"/> are the list facets;
/// results are always customized-first then built-in (FR-6.1), each set ordered by <see cref="Sort"/>.
/// </summary>
public sealed record TemplateSearchQuery(
    string? Q = null,
    TemplateClass? Class = null,
    string? Sector = null,
    string Sort = "updated_at",
    string Order = "desc",
    int PageSize = 50,
    string? PageToken = null);
