using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Appearance.Interfaces;

/// <summary>
/// Data-access port for the per-survey theme (DB-08, 1:1 with a survey). Implemented by
/// <c>ThemeStore</c> (T066). <see cref="GetModeAsync"/> reads the owning survey's
/// <c>theme_mode</c> to decide whether appearance resolves from the tenant guidelines or the theme.
/// </summary>
public interface IThemeStore
{
    Task<Theme?> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    Task<ThemeMode> GetModeAsync(Guid surveyId, CancellationToken ct = default);

    Task UpsertAsync(Theme theme, CancellationToken ct = default);
}
