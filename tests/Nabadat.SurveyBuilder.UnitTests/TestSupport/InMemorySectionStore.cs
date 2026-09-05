using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the section aggregate. <b>Provisional (T032)</b> — declare
/// <c>: ISectionStore</c> and align the method surface when that port lands with its service (US1).
/// </summary>
public sealed class InMemorySectionStore
{
    public Dictionary<Guid, Section> Items { get; } = new();

    public Task AddAsync(Section section)
    {
        Items[section.Id] = section;
        return Task.CompletedTask;
    }

    public Task<Section?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var section);
        return Task.FromResult(section);
    }
}
