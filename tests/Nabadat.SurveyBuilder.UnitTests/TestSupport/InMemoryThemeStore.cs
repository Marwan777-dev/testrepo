using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the per-survey theme aggregate. <b>Provisional (T032)</b> — declare
/// <c>: IThemeStore</c> and align the method surface when that port lands (US1 Appearance).
/// </summary>
public sealed class InMemoryThemeStore
{
    public Dictionary<Guid, Theme> Items { get; } = new();

    public Task AddAsync(Theme theme)
    {
        Items[theme.Id] = theme;
        return Task.CompletedTask;
    }

    public Task<Theme?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var theme);
        return Task.FromResult(theme);
    }
}
