using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// The F1 Survey Library filter (contracts/surveys.md GET /surveys). All filters combine with AND;
/// <see cref="Q"/> matches <c>LOWER(name_en)</c>. Cursor pagination per API-04.
/// </summary>
public sealed record SurveySearchQuery(
    string? Q = null,
    IReadOnlyList<SurveyType>? Types = null,
    IReadOnlyList<SurveyStatus>? Statuses = null,
    Guid? JourneyId = null,
    string Sort = "updated_at",
    string Order = "desc",
    int PageSize = 50,
    string? PageToken = null);
