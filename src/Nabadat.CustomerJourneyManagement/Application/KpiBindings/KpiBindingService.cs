using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.KpiBindings;

/// <summary>
/// The touchpoint KPI-binding application service (T047 / US-2). Owns the full-replace save behind
/// <c>PUT /api/v1/touchpoints/{id}/kpis</c> (<c>contracts/configuration-api.md</c>): the request body
/// is the complete, authoritative binding set for a touchpoint, and this service replaces the
/// touchpoint's bindings atomically (DELETE + INSERT in one transaction).
/// <list type="bullet">
///   <item><description>
///     <b>Guards run before any write.</b> The touchpoint must exist, its parent stage and journey
///     must exist, and the journey must not be <see cref="JourneyStatus.Archived"/> — Archived journeys
///     are immutable (<c>journey.archived_immutable</c>). All three are checked before the transaction
///     opens, so a rejected save writes nothing.
///   </description></item>
///   <item><description>
///     <b>Weights are validated first.</b> <see cref="IKpiWeightValidator"/> (T045) enforces the
///     <c>(0,100]</c>-per-weight, no-duplicate-type, known-type, and sum-=-100.00m rules; on failure the
///     service returns the validator's API-05 code and performs no DB write (FR — "no partial state").
///   </description></item>
///   <item><description>
///     <b>Persist + audit + report-contract rebuild are one transaction</b> (FR-015):
///     <see cref="ITouchpointDataService.ReplaceKpiBindingsAsync"/>, the
///     <c>journey.kpi_bindings.updated</c> M-17 event, and
///     <see cref="ReportContractService.RebuildContractAsync"/> all commit together (the report-contract
///     rebuild is a Phase-2 no-op stub, real in T087/US-4).
///   </description></item>
/// </list>
/// </summary>
public sealed class KpiBindingService
{
    /// <summary>The NPS key — its presence in the saved set raises the non-blocking <c>npsWarning</c> flag.</summary>
    private static readonly string NpsKey = nameof(PlatformKpiType.NPS);

    private readonly ITouchpointDataService _touchpoints;
    private readonly IStageDataService _stages;
    private readonly IJourneyDataService _journeys;
    private readonly IActiveKpiCatalogReader _catalog;
    private readonly IKpiWeightValidator _weightValidator;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly ReportContractService _reportContracts;
    private readonly TimeProvider _time;

    public KpiBindingService(
        ITouchpointDataService touchpoints,
        IStageDataService stages,
        IJourneyDataService journeys,
        IActiveKpiCatalogReader catalog,
        IKpiWeightValidator weightValidator,
        ITenantDbContext db,
        IM17EventPublisher events,
        ReportContractService reportContracts,
        TimeProvider time)
    {
        _touchpoints = touchpoints;
        _stages = stages;
        _journeys = journeys;
        _catalog = catalog;
        _weightValidator = weightValidator;
        _db = db;
        _events = events;
        _reportContracts = reportContracts;
        _time = time;
    }

    /// <summary>
    /// Full-replaces the KPI bindings on <paramref name="touchpointId"/>. On success returns the saved
    /// set with its derived <c>isMeasured</c>/<c>npsWarning</c> flags (row replace + M-17 event +
    /// report-contract rebuild committed in one tx). On failure returns one of
    /// <c>journey.touchpoint_not_found</c>, <c>journey.stage_not_found</c>, <c>journey.not_found</c>,
    /// <c>journey.archived_immutable</c>, or a weight-validation code
    /// (<c>kpi.weight_sum_invalid</c> / <c>kpi.duplicate_type</c> / <c>kpi.unknown_type</c> /
    /// <c>kpi.individual_weight_invalid</c>) — and writes nothing.
    /// </summary>
    public async Task<ServiceResult<SaveKpiBindingsResult>> SaveKpiBindingsAsync(
        Guid touchpointId,
        IReadOnlyList<KpiBindingInput> bindings,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var touchpoint = await _touchpoints.GetByIdAsync(touchpointId, ct);
        if (touchpoint is null)
        {
            return ServiceResult<SaveKpiBindingsResult>.Failure(
                "journey.touchpoint_not_found", $"Touchpoint {touchpointId} does not exist.");
        }

        var stage = await _stages.GetByIdAsync(touchpoint.StageId, ct);
        if (stage is null)
        {
            return ServiceResult<SaveKpiBindingsResult>.Failure(
                "journey.stage_not_found", $"Stage {touchpoint.StageId} does not exist.");
        }

        var journey = await _journeys.GetByIdAsync(stage.JourneyId, ct);
        if (journey is null)
        {
            return ServiceResult<SaveKpiBindingsResult>.Failure(
                "journey.not_found", $"Journey {stage.JourneyId} does not exist.");
        }

        if (string.Equals(journey.Status, JourneyStatus.Archived.ToString(), StringComparison.Ordinal))
        {
            return ServiceResult<SaveKpiBindingsResult>.Failure(
                "journey.archived_immutable", "Archived journeys are immutable and cannot be edited.");
        }

        // Validate the full set before touching the database — no partial state on a rejected save.
        var validation = await _weightValidator.ValidateAsync(bindings, ct);
        if (!validation.IsSuccess)
        {
            return ServiceResult<SaveKpiBindingsResult>.Failure(
                validation.Error!.Code, validation.Error.Message);
        }

        // Resolve each requested type against the active catalogue so the binding records both its
        // platform-standard flag and the M-06 kpi_id link (the FR-026/FR-017 binding-usage probe counts
        // touchpoints by kpi_id). Validation above guarantees every key is present in the catalogue;
        // when the standalone default reader supplies the catalogue the entries carry no id, so kpi_id
        // stays null — the pre-integration behaviour.
        var catalogByKey = (await _catalog.GetActiveKpisAsync(ct))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var now = _time.GetUtcNow();
        var entities = bindings
            .Select(binding =>
            {
                catalogByKey.TryGetValue(binding.KpiType, out var entry);
                return new KpiBinding
                {
                    KpiBindingId = Guid.NewGuid(),
                    TouchpointId = touchpointId,
                    KpiType = binding.KpiType,
                    IsPlatformStandard = entry?.IsPlatformStandard ?? false,
                    KpiId = entry?.KpiId,
                    Weight = binding.Weight,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
            })
            .ToList();

        await _db.ExecuteAsync(async () =>
        {
            // Full replace: the data service DELETEs the existing rows then INSERTs the authoritative
            // set (empty set ⇒ unmeasured touchpoint). DELETE + INSERTs are atomic on this transaction.
            await _touchpoints.ReplaceKpiBindingsAsync(touchpointId, entities, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyKpiBindingsUpdated(
                    actor.UserId,
                    actor.Persona,
                    journey.JourneyId,
                    now,
                    actor.CorrelationId,
                    newValue: new
                    {
                        touchpointId,
                        bindings = entities.Select(e => new { e.KpiType, e.Weight }),
                    }),
                ct);

            // Keep M-07's report contract in step with the new bindings — rebuilt on this same
            // transaction (FR-015) so the bindings and their contract projection commit together.
            await _reportContracts.RebuildContractAsync(journey.JourneyId, ct);
        }, ct);

        var isMeasured = entities.Count > 0;
        var npsWarning = entities.Any(e => string.Equals(e.KpiType, NpsKey, StringComparison.Ordinal));

        return ServiceResult<SaveKpiBindingsResult>.Success(
            new SaveKpiBindingsResult(touchpointId, entities, isMeasured, npsWarning, now));
    }
}

/// <summary>
/// Outcome of a touchpoint KPI-binding full-replace save (<c>PUT /api/v1/touchpoints/{id}/kpis</c>
/// 200 body). <see cref="IsMeasured"/> is <c>true</c> when the saved set is non-empty (a touchpoint
/// with no bindings is unmeasured and excluded from scoring, FR-008); <see cref="NpsWarning"/> is
/// <c>true</c> when <c>NPS</c> is in the set — a non-blocking informational flag the UI surfaces as a
/// survey-distribution reminder (the response is still 200).
/// </summary>
/// <param name="TouchpointId">The touchpoint whose bindings were saved.</param>
/// <param name="KpiBindings">The persisted authoritative binding set (empty when unmeasured).</param>
/// <param name="IsMeasured">True when the set is non-empty.</param>
/// <param name="NpsWarning">True when <c>NPS</c> is among the saved bindings.</param>
/// <param name="UpdatedAt">When the save committed.</param>
public sealed record SaveKpiBindingsResult(
    Guid TouchpointId,
    IReadOnlyList<KpiBinding> KpiBindings,
    bool IsMeasured,
    bool NpsWarning,
    DateTimeOffset UpdatedAt);
