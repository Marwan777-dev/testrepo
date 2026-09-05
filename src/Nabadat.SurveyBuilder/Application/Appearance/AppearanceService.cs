using Nabadat.SurveyBuilder.Application.Appearance.Dtos;
using Nabadat.SurveyBuilder.Application.Appearance.Interfaces;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Appearance;

/// <summary>
/// F4 appearance service (T080): resolves the effective tokens (Inherited ⇒ from the M-11
/// <see cref="ITenantDesignGuidelinesReader"/>; Customize ⇒ from the survey's <c>Theme</c>) and
/// saves a Customize theme (validating that an Image background carries a file handle). Logo upload
/// via <c>IFileStorageService</c> is wired when the shared adapter ships (TODO-M01-006).
/// </summary>
public sealed class AppearanceService
{
    private readonly IThemeStore _themes;
    private readonly ITenantDesignGuidelinesReader _guidelines;
    private readonly ITenantDbContext _context;
    private readonly TimeProvider _timeProvider;

    public AppearanceService(
        IThemeStore themes,
        ITenantDesignGuidelinesReader guidelines,
        ITenantDbContext context,
        TimeProvider timeProvider)
    {
        _themes = themes;
        _guidelines = guidelines;
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<ResolvedAppearance> ResolveAsync(Guid surveyId, CancellationToken ct = default)
    {
        var mode = await _themes.GetModeAsync(surveyId, ct);
        if (mode == ThemeMode.Inherited)
        {
            var guidelines = await _guidelines.GetDesignGuidelinesAsync(ct);
            return new ResolvedAppearance(guidelines.PrimaryColour, guidelines.TextColour, guidelines.ButtonRadiusPx);
        }

        var theme = await _themes.GetBySurveyAsync(surveyId, ct);
        var fallback = await _guidelines.GetDesignGuidelinesAsync(ct);
        return new ResolvedAppearance(
            theme?.PrimaryColor ?? fallback.PrimaryColour,
            theme?.TextColor ?? fallback.TextColour,
            theme?.ButtonRadiusPx ?? fallback.ButtonRadiusPx);
    }

    public async Task<AppearanceSaveResult> SaveAsync(SaveThemeCommand command, CancellationToken ct = default)
    {
        if (command.BackgroundType == BackgroundType.Image && string.IsNullOrEmpty(command.BackgroundImageHandle))
        {
            return AppearanceSaveResult.Invalid("theme.background_image.required");
        }

        var now = _timeProvider.GetUtcNow();
        await _context.ExecuteAsync(async () =>
        {
            var theme = await _themes.GetBySurveyAsync(command.SurveyId, ct) ?? new Theme
            {
                Id = Guid.NewGuid(),
                SurveyId = command.SurveyId,
                CreatedAt = now,
            };

            theme.PrimaryColor = command.PrimaryColour;
            theme.BackgroundType = command.BackgroundType;
            theme.BackgroundConfig = command.BackgroundType == BackgroundType.Image
                ? new BackgroundConfig(FileHandle: command.BackgroundImageHandle)
                : theme.BackgroundConfig;
            theme.UpdatedAt = now;
            theme.IncrementRowVersion();

            await _themes.UpsertAsync(theme, ct);
        }, ct);

        return AppearanceSaveResult.Valid();
    }
}
