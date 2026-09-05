using Nabadat.SurveyBuilder.Application.Appearance;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>F4 resolved appearance view (contracts/surveys.md GET /surveys/{id}/theme).</summary>
public sealed record ThemeView(string PrimaryColour, string? TextColour, int? ButtonRadiusPx)
{
    public static ThemeView From(ResolvedAppearance a) => new(a.PrimaryColour, a.TextColour, a.ButtonRadiusPx);
}
