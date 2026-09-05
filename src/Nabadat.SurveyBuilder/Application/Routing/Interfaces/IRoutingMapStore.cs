using Nabadat.SurveyBuilder.Domain.Entities;

namespace Nabadat.SurveyBuilder.Application.Routing.Interfaces;

/// <summary>
/// Data-access port for the sparse per-answer routing overrides (data-model.md §2.5, F9).
/// Implemented by <c>RoutingMapStore</c> (T170) over <c>ITenantDbContext</c>. Only overrides are
/// persisted — defaults (next-in-order) are computed by <c>RoutingDefaultTargeter</c> and never
/// stored (research.md §6). Consumed by <c>RoutingConfigurationService</c> (T175):
/// <see cref="GetBySourceQuestionAsync"/> backs the GET routing editor,
/// <see cref="DeleteBySourceQuestionAsync"/> + <see cref="AddAsync"/> back the replace-map save,
/// and <see cref="DeleteByTargetQuestionAsync"/> backs the FR-2.7 reset-to-default on question
/// delete (routes pointing at the deleted question are removed so the default reapplies).
/// </summary>
public interface IRoutingMapStore
{
    /// <summary>All override routes whose source is <paramref name="sourceQuestionId"/> (one per answer key).</summary>
    Task<IReadOnlyList<RoutingMap>> GetBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default);

    /// <summary>All override routes in a survey — powers the render-plan routing projection (contracts/surveys.md).</summary>
    Task<IReadOnlyList<RoutingMap>> GetBySurveyAsync(Guid surveyId, CancellationToken ct = default);

    Task AddAsync(RoutingMap routingMap, CancellationToken ct = default);

    Task UpdateAsync(RoutingMap routingMap, CancellationToken ct = default);

    /// <summary>Removes every override for one source question — used to replace a question's whole map atomically.</summary>
    Task DeleteBySourceQuestionAsync(Guid sourceQuestionId, CancellationToken ct = default);

    /// <summary>Removes every override pointing at <paramref name="targetQuestionId"/> (FR-2.7 reset-to-default on delete).</summary>
    Task DeleteByTargetQuestionAsync(Guid targetQuestionId, CancellationToken ct = default);
}
