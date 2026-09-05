using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the survey aggregate, used by service unit tests instead of a real
/// EF context (CLAUDE.md rule 14 — fakes for stateful collaborators).
/// <para><b>Provisional (T032).</b> The <c>ISurveyStore</c> port is defined with its service in US1;
/// when it lands this class should declare <c>: ISurveyStore</c> and align its method surface. The
/// dictionary keyed by <see cref="Survey.Id"/> is the backing state a test seeds and asserts.</para>
/// </summary>
public sealed class InMemorySurveyStore
{
    public Dictionary<Guid, Survey> Items { get; } = new();

    public Task AddAsync(Survey survey)
    {
        Items[survey.Id] = survey;
        return Task.CompletedTask;
    }

    public Task<Survey?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var survey);
        return Task.FromResult(survey);
    }
}
