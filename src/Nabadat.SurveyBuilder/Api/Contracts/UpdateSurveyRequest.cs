using Nabadat.SurveyBuilder.Application.Surveys.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>PUT /api/v1/surveys/{id} body (contracts/surveys.md) — POST shape minus <c>id</c>.</summary>
public sealed record UpdateSurveyRequest(
    string? NameEn,
    string? Description,
    Guid? BoundJourneyId,
    string? WelcomeHtml,
    string? ThanksHtml,
    string? RedirectUrl,
    int RedirectAfterS = 0,
    LayoutMode Layout = LayoutMode.Section,
    int? QuestionsPerPage = null,
    ActivePeriod? ActivePeriod = null,
    bool Shuffle = false,
    string ShuffleMode = "random",
    bool RoutingOn = false,
    ThemeMode ThemeMode = ThemeMode.Inherited)
{
    public SurveyDraft ToDraft() => new()
    {
        NameEn = NameEn,
        Description = Description,
        BoundJourney = BoundJourneyId,
        WelcomeHtml = WelcomeHtml,
        ThanksHtml = ThanksHtml,
        RedirectUrl = RedirectUrl,
        RedirectAfterS = RedirectAfterS,
        Layout = Layout,
        QuestionsPerPage = QuestionsPerPage,
        ActivePeriod = ActivePeriod,
        Shuffle = Shuffle,
        ShuffleMode = ShuffleMode,
        RoutingOn = RoutingOn,
        ThemeMode = ThemeMode,
    };
}
