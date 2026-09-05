using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// The per-survey appearance (F4) inside a <see cref="SurveySnapshot"/> — present only when the
/// source survey was in Customize mode. Copied whole on save-as-template (FR-7.4) and recreated as
/// a fresh <c>Theme</c> row on instantiate (BR-7.1). Mirrors the <c>themes</c> columns
/// (data-model.md §2.6).
/// </summary>
public sealed record ThemeSnapshot(
    string? PrimaryColor,
    string? TextColor,
    int? ButtonRadiusPx,
    string? ButtonBorderColor,
    string? ButtonTextColor,
    bool HeaderShowLogo,
    bool HeaderShowTitle,
    string HeaderAlignment,
    string? FooterText,
    BackgroundType BackgroundType,
    BackgroundConfig? BackgroundConfig,
    int BackgroundOpacity,
    string? AdvancedStatusColors,
    string? AdvancedSurfaces,
    string? AdvancedTypography,
    string? AdvancedLayout);
