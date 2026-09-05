using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the questions-set aggregate. <b>Provisional (T032)</b> — declare
/// <c>: IQuestionsSetStore</c> and align the method surface when that port lands (US1).
/// </summary>
public sealed class InMemoryQuestionsSetStore
{
    public Dictionary<Guid, QuestionsSet> Items { get; } = new();

    public Task AddAsync(QuestionsSet set)
    {
        Items[set.Id] = set;
        return Task.CompletedTask;
    }

    public Task<QuestionsSet?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var set);
        return Task.FromResult(set);
    }
}
