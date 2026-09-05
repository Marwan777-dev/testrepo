using Nabadat.SurveyBuilder.Application.Appearance.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>PUT /api/v1/surveys/{id}/theme body (F4 Customize save).</summary>
public sealed record UpdateThemeRequest(
    ThemeMode Mode,
    BackgroundType BackgroundType,
    string? BackgroundImageHandle,
    string? PrimaryColour)
{
    public SaveThemeCommand ToCommand(Guid surveyId) =>
        new(surveyId, Mode, BackgroundType, BackgroundImageHandle, PrimaryColour);
}
