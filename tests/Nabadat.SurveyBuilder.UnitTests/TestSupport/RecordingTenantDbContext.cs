using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// A test double for <see cref="ITenantDbContext"/> that verifies the transaction boundary: its
/// <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> runs the delegate and increments
/// <see cref="ExecuteAsyncCallCount"/>, so a service test can assert the service wrapped its
/// multi-write operation in <c>ExecuteAsync</c> (data-model.md §8 atomicity). Data access goes
/// through the in-memory stores instead, so the <see cref="DbSet{TEntity}"/> members are not
/// implemented — they throw to catch a test that reaches for them by mistake.
/// </summary>
public sealed class RecordingTenantDbContext : ITenantDbContext
{
    /// <summary>Number of times a service opened the <see cref="ExecuteAsync(Func{Task}, CancellationToken)"/> boundary.</summary>
    public int ExecuteAsyncCallCount { get; private set; }

    private static NotSupportedException NoDbSets() => new(
        "RecordingTenantDbContext exposes no DbSets — use the in-memory stores for data and assert " +
        "ExecuteAsyncCallCount for the transaction boundary.");

    public DbSet<Survey> Surveys => throw NoDbSets();
    public DbSet<Section> Sections => throw NoDbSets();
    public DbSet<QuestionsSet> QuestionsSets => throw NoDbSets();
    public DbSet<Question> Questions => throw NoDbSets();
    public DbSet<RoutingMap> RoutingMaps => throw NoDbSets();
    public DbSet<Theme> Themes => throw NoDbSets();
    public DbSet<SurveyTranslation> SurveyTranslations => throw NoDbSets();
    public DbSet<Template> Templates => throw NoDbSets();
    public DbSet<TemplateSnapshot> TemplateSnapshots => throw NoDbSets();
    public DatabaseFacade Database => throw NoDbSets();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public async Task ExecuteAsync(Func<Task> work, CancellationToken ct = default)
    {
        ExecuteAsyncCallCount++;
        await work();
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> work, CancellationToken ct = default)
    {
        ExecuteAsyncCallCount++;
        return await work();
    }
}
