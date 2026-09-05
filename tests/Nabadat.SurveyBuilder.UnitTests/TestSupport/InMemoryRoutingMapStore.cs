using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.UnitTests.TestSupport;

/// <summary>
/// In-memory stateful fake for the routing-map port (<see cref="IRoutingMapStore"/>, T170).
/// Backs <c>RoutingConfigurationService</c> unit tests (US4) — replace-map save, per-source /
/// per-survey reads, and the FR-2.7 target-invalidation delete.
/// </summary>
public sealed class InMemoryRoutingMapStore : IRoutingMapStore
{
    public Dictionary<Guid, RoutingMap> Items { get; } = new();

    public Task<IReadOnlyList<RoutingMap>> GetBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RoutingMap>>(
            Items.Values.Where(r => r.SourceQuestionId == sourceQuestionId).ToList());

    public Task<IReadOnlyList<RoutingMap>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RoutingMap>>(
            Items.Values.Where(r => r.SurveyId == surveyId).ToList());

    public Task AddAsync(RoutingMap routingMap, CancellationToken ct = default)
    {
        Items[routingMap.Id] = routingMap;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RoutingMap routingMap, CancellationToken ct = default)
    {
        Items[routingMap.Id] = routingMap;
        return Task.CompletedTask;
    }

    public Task DeleteBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default)
    {
        foreach (var id in Items.Values.Where(r => r.SourceQuestionId == sourceQuestionId).Select(r => r.Id).ToList())
        {
            Items.Remove(id);
        }

        return Task.CompletedTask;
    }

    public Task DeleteByTargetQuestionAsync(Guid targetQuestionId, CancellationToken ct = default)
    {
        foreach (var id in Items.Values.Where(r => r.TargetQuestionId == targetQuestionId).Select(r => r.Id).ToList())
        {
            Items.Remove(id);
        }

        return Task.CompletedTask;
    }
}
