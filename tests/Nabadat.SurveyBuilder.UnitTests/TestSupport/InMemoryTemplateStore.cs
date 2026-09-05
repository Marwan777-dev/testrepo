using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the template aggregate. <b>Provisional (T032)</b> — declare
/// <c>: ITemplateStore</c> and align the method surface when that port lands (US5).
/// </summary>
public sealed class InMemoryTemplateStore
{
    public Dictionary<Guid, Template> Items { get; } = new();

    public Task AddAsync(Template template)
    {
        Items[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task<Template?> GetAsync(Guid id)
    {
        Items.TryGetValue(id, out var template);
        return Task.FromResult(template);
    }
}
