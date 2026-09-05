namespace Nabadat.SurveyBuilder.Application.Appearance;

/// <summary>
/// The effective appearance tokens for a survey (F4) — resolved from the tenant design guidelines
/// (Inherited mode) or the survey's own <c>Theme</c> (Customize mode). Returned by
/// <c>AppearanceService.ResolveAsync</c> (T080). Only the tokens M-01 renders are surfaced.
/// </summary>
public sealed record ResolvedAppearance(
    string PrimaryColour,
    string? TextColour = null,
    int? ButtonRadiusPx = null);
