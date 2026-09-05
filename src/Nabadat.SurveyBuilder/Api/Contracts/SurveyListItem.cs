using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>One F1 Survey Library row (contracts/surveys.md GET /surveys).</summary>
public sealed record SurveyListItem(
    Guid Id,
    string NameEn,
    SurveyType SurveyType,
    Guid? BoundJourneyId,
    SurveyStatus Status,
    int RulesCount,
    ThemeMode ThemeMode,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy)
{
    public static SurveyListItem From(Survey s, int rulesCount) => new(
        s.Id, s.NameEn, s.SurveyType, s.BoundJourneyId, s.Status, rulesCount, s.ThemeMode, s.UpdatedAt, s.UpdatedBy);
}
