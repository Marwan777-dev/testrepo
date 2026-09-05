using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>Full survey view (contracts/surveys.md GET /surveys/{id}, POST/PUT response body).</summary>
public sealed record SurveyView(
    Guid Id,
    string NameEn,
    string? Description,
    SurveyType SurveyType,
    Guid? BoundJourneyId,
    SurveyStatus Status,
    ThemeMode ThemeMode,
    string? WelcomeHtml,
    string? ThanksHtml,
    string? RedirectUrl,
    int RedirectAfterS,
    LayoutMode Layout,
    int? QuestionsPerPage,
    ActivePeriod? ActivePeriod,
    bool Shuffle,
    string ShuffleMode,
    bool RoutingOn,
    bool ShuffleLocked,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    int RowVersion)
{
    public static SurveyView From(Survey s) => new(
        s.Id, s.NameEn, s.Description, s.SurveyType, s.BoundJourneyId, s.Status, s.ThemeMode,
        s.WelcomeHtml, s.ThanksHtml, s.RedirectUrl, s.RedirectAfterS, s.Layout, s.QuestionsPerPage,
        s.ActivePeriod, s.Shuffle, s.ShuffleMode, s.RoutingOn, s.ShuffleLocked, s.UpdatedAt, s.UpdatedBy, s.RowVersion);
}
