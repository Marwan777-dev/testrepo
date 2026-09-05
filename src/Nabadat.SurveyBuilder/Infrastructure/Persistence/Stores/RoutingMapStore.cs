using Microsoft.EntityFrameworkCore;
using Nabadat.SurveyBuilder.Application.Interfaces;
using Nabadat.SurveyBuilder.Application.Routing.Interfaces;
using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Infrastructure.Persistence.Stores;

/// <summary>EF implementation of <see cref="IRoutingMapStore"/> (T170) over <see cref="ITenantDbContext"/>.</summary>
public sealed class RoutingMapStore : IRoutingMapStore
{
    private readonly ITenantDbContext _context;

    public RoutingMapStore(ITenantDbContext context) => _context = context;

    public async Task<IReadOnlyList<RoutingMap>> GetBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default) =>
        await _context.RoutingMaps.Where(r => r.SourceQuestionId == sourceQuestionId).ToListAsync(ct);

    public async Task<IReadOnlyList<RoutingMap>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default) =>
        await _context.RoutingMaps.Where(r => r.SurveyId == surveyId).ToListAsync(ct);

    public Task AddAsync(RoutingMap routingMap, CancellationToken ct = default)
    {
        _context.RoutingMaps.Add(routingMap);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RoutingMap routingMap, CancellationToken ct = default)
    {
        _context.RoutingMaps.Update(routingMap);
        return Task.CompletedTask;
    }

    public async Task DeleteBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default)
    {
        var routes = await _context.RoutingMaps.Where(r => r.SourceQuestionId == sourceQuestionId).ToListAsync(ct);
        _context.RoutingMaps.RemoveRange(routes);
    }

    public async Task DeleteByTargetQuestionAsync(Guid targetQuestionId, CancellationToken ct = default)
    {
        var routes = await _context.RoutingMaps.Where(r => r.TargetQuestionId == targetQuestionId).ToListAsync(ct);
        _context.RoutingMaps.RemoveRange(routes);
    }
}
