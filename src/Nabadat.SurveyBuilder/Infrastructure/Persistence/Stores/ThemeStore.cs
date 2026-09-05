using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Appearance.Interfaces;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>EF implementation of <see cref="IThemeStore"/> (T066) over <see cref="ITenantDbContext"/>.</summary>
public sealed class ThemeStore : IThemeStore
{
    private readonly ITenantDbContext _context;

    public ThemeStore(ITenantDbContext context) => _context = context;

    public Task<Theme?> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        _context.Themes.FirstOrDefaultAsync(t => t.SurveyId == surveyId, ct);

    public async Task<ThemeMode> GetModeAsync(Guid surveyId, CancellationToken ct = default)
    {
        var survey = await _context.Surveys.FirstOrDefaultAsync(s => s.Id == surveyId, ct);
        return survey?.ThemeMode ?? ThemeMode.Inherited;
    }

    public async Task UpsertAsync(Theme theme, CancellationToken ct = default)
    {
        var existing = await _context.Themes.FirstOrDefaultAsync(t => t.SurveyId == theme.SurveyId, ct);
        if (existing is null)
        {
            _context.Themes.Add(theme);
        }
        else
        {
            _context.Themes.Update(theme);
        }
    }
}
