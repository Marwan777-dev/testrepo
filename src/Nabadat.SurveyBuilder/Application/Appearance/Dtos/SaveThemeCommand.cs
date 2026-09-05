using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Appearance.Dtos;

/// <summary>
/// F4 appearance-save input (T080). In <see cref="ThemeMode.Customized"/> mode the tokens persist to
/// the survey's <c>themes</c> row; an <see cref="BackgroundType.Image"/> background requires
/// <see cref="BackgroundImageHandle"/> (<c>theme.background_image.required</c>).
/// </summary>
public sealed record SaveThemeCommand(
    Guid SurveyId,
    ThemeMode Mode,
    BackgroundType BackgroundType,
    string? BackgroundImageHandle,
    string? PrimaryColour);
