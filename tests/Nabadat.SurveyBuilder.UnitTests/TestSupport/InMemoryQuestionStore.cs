using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the question aggregate. <b>Provisional (T032)</b> — declare
/// <c>: IQuestionStore</c> and align the method surface when that port lands (US1).
/// </summary>
public sealed class InMemoryQuestionStore
{
    public Dictionary<Guid, Question> Items { get; } = new();

    public Task AddAsync(Question question)
    {
        Items[question.Id] = question;
        return Task.CompletedTask;
    }

    public Task<Question?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var question);
        return Task.FromResult(question);
    }
}
