using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the survey-translation aggregate. <b>Provisional (T032)</b> —
/// declare <c>: ITranslationStore</c> and align the method surface when that port lands (US6).
/// </summary>
public sealed class InMemoryTranslationStore
{
    public Dictionary<Guid, SurveyTranslation> Items { get; } = new();

    public Task AddAsync(SurveyTranslation translation)
    {
        Items[translation.Id] = translation;
        return Task.CompletedTask;
    }

    public Task<SurveyTranslation?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var translation);
        return Task.FromResult(translation);
    }
}
