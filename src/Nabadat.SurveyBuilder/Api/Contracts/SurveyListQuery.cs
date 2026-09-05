using Microsoft.AspNetCore.Mvc;
using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// Query-string binding for GET /api/v1/surveys (contracts/surveys.md). <c>type</c>/<c>status</c>
/// are comma-separated; unknown values raise <c>survey.filter.invalid</c> when converted.
/// </summary>
public sealed class SurveyListQuery
{
    [FromQuery(Name = "q")] public string? Q { get; init; }

    [FromQuery(Name = "type")] public string? Type { get; init; }

    [FromQuery(Name = "status")] public string? Status { get; init; }

    [FromQuery(Name = "journey_id")] public Guid? JourneyId { get; init; }

    [FromQuery(Name = "sort")] public string Sort { get; init; } = "updated_at";

    [FromQuery(Name = "order")] public string Order { get; init; } = "desc";

    [FromQuery(Name = "page_size")] public int PageSize { get; init; } = 50;

    [FromQuery(Name = "page_token")] public string? PageToken { get; init; }

    public SurveySearchQuery ToSearchQuery() => new(
        Q,
        ParseCsv<SurveyType>(Type),
        ParseCsv<SurveyStatus>(Status),
        JourneyId,
        Sort,
        Order,
        PageSize,
        PageToken);

    private static IReadOnlyList<TEnum>? ParseCsv<TEnum>(string? csv) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return null;
        }

        var values = new List<TEnum>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse<TEnum>(part, ignoreCase: true, out var parsed))
            {
                throw new Application.Exceptions.SurveyBuilderException(
                    "survey.filter.invalid", 400, $"Unknown filter value '{part}'.");
            }

            values.Add(parsed);
        }

        return values;
    }
}
