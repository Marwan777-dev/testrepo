using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Reports;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Detection;

/// <summary>
/// The journey detection-config application service (T085 / US-4). Owns the save behind
/// <c>PUT /api/v1/journeys/{id}/detection</c> (<c>contracts/configuration-api.md</c>): the request body
/// is the complete, authoritative pain/happy configuration for a journey — the journey-level thresholds
/// plus the full set of per-stage and per-touchpoint overrides — and this service replaces it atomically.
/// <list type="bullet">
///   <item><description>
///     <b>Validation runs before any write</b> (no partial state on a rejected save): every threshold —
///     the journey-level pair and every non-null override value — must lie in <c>[0, 100]</c>
///     (<c>detection.out_of_range</c>); the journey-level pair must be strictly ordered
///     <c>pain &lt; happy</c> (<c>detection.threshold_invalid</c>); and every override
///     <see cref="DetectionOverrideInput.ScopeId"/> must belong to the journey
///     (<c>detection.unknown_stage</c> / <c>detection.unknown_touchpoint</c>).
///   </description></item>
///   <item><description>
///     <b>One detection config per journey.</b> The service loads-or-creates the journey's single
///     <see cref="DetectionConfig"/> id and reuses it as the FK for the override rows, so a re-save
///     keeps the same <c>detection_config_id</c>.
///   </description></item>
///   <item><description>
///     <b>Persist + audit + report-contract rebuild are one transaction</b> (FR-015): the config upsert,
///     the full-replace of its overrides, the <c>journey.detection_config.updated</c> M-17 event, and
///     <see cref="ReportContractService.RebuildContractAsync"/> all commit together (the report-contract
///     rebuild is a Phase-2 no-op stub, real in T087/US-4).
///   </description></item>
/// </list>
/// </summary>
public sealed class DetectionConfigService
{
    private const decimal MinThreshold = 0m;
    private const decimal MaxThreshold = 100m;
    private const string StageScope = "stage";
    private const string TouchpointScope = "touchpoint";

    private readonly IDetectionDataService _detection;
    private readonly IStageDataService _stages;
    private readonly ITouchpointDataService _touchpoints;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly ReportContractService _reportContracts;
    private readonly TimeProvider _time;

    public DetectionConfigService(
        IDetectionDataService detection,
        IStageDataService stages,
        ITouchpointDataService touchpoints,
        ITenantDbContext db,
        IM17EventPublisher events,
        ReportContractService reportContracts,
        TimeProvider time)
    {
        _detection = detection;
        _stages = stages;
        _touchpoints = touchpoints;
        _db = db;
        _events = events;
        _reportContracts = reportContracts;
        _time = time;
    }

    /// <summary>
    /// Saves the journey's detection configuration. On success returns the persisted config plus the
    /// override counts (config upsert + override full-replace + M-17 event + report-contract rebuild
    /// committed in one tx). On failure returns one of <c>detection.out_of_range</c>,
    /// <c>detection.threshold_invalid</c>, <c>detection.unknown_stage</c>, or
    /// <c>detection.unknown_touchpoint</c> — and writes nothing.
    /// </summary>
    public async Task<ServiceResult<SaveDetectionConfigResult>> SaveDetectionConfigAsync(
        Guid journeyId,
        SaveDetectionConfigInput input,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // --- Validate before any write — a rejected save must leave no partial state. ---
        // Range [0,100] applies to the journey-level pair AND every non-null override value
        // ("Any threshold value < 0 or > 100" — contract).
        if (!InRange(input.PainThreshold)
            || !InRange(input.HappyThreshold)
            || input.StageOverrides.Any(OutOfRange)
            || input.TouchpointOverrides.Any(OutOfRange))
        {
            return ServiceResult<SaveDetectionConfigResult>.Failure(
                "detection.out_of_range", "Detection thresholds must be between 0 and 100.");
        }

        // The neutral band requires a strictly-ordered journey-level pair.
        if (input.PainThreshold >= input.HappyThreshold)
        {
            return ServiceResult<SaveDetectionConfigResult>.Failure(
                "detection.threshold_invalid", "painThreshold must be strictly less than happyThreshold.");
        }

        // Every override scope (polymorphic, no FK) must resolve to a stage/touchpoint of THIS journey.
        var journeyStageIds = (await _stages.ListByJourneyAsync(journeyId, ct))
            .Select(s => s.StageId)
            .ToHashSet();

        if (input.StageOverrides.Any(o => !journeyStageIds.Contains(o.ScopeId)))
        {
            return ServiceResult<SaveDetectionConfigResult>.Failure(
                "detection.unknown_stage", "A stage override references a stage that is not in this journey.");
        }

        foreach (var touchpointOverride in input.TouchpointOverrides)
        {
            var touchpoint = await _touchpoints.GetByIdAsync(touchpointOverride.ScopeId, ct);
            if (touchpoint is null || !journeyStageIds.Contains(touchpoint.StageId))
            {
                return ServiceResult<SaveDetectionConfigResult>.Failure(
                    "detection.unknown_touchpoint",
                    "A touchpoint override references a touchpoint that is not in this journey.");
            }
        }

        // --- Load-or-create the journey's single detection config, then persist atomically. ---
        var existing = await _detection.GetByJourneyAsync(journeyId, ct);
        var now = _time.GetUtcNow();
        var configId = existing?.DetectionConfigId ?? Guid.NewGuid();

        var config = new DetectionConfig
        {
            DetectionConfigId = configId,
            JourneyId = journeyId,
            PainThreshold = input.PainThreshold,
            HappyThreshold = input.HappyThreshold,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
        };

        var overrides = BuildOverrides(configId, input, now);

        await _db.ExecuteAsync(async () =>
        {
            // Config upsert + override full-replace + audit event + report rebuild are one unit of work.
            await _detection.UpsertConfigAsync(config, ct);
            await _detection.ReplaceOverridesAsync(configId, overrides, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyDetectionConfigUpdated(
                    actor.UserId,
                    actor.Persona,
                    journeyId,
                    now,
                    actor.CorrelationId,
                    newValue: new
                    {
                        config.PainThreshold,
                        config.HappyThreshold,
                        stageOverrideCount = input.StageOverrides.Count,
                        touchpointOverrideCount = input.TouchpointOverrides.Count,
                    }),
                ct);

            // Keep M-07's report contract in step with the new thresholds — rebuilt on this same
            // transaction (FR-015) so the config and its contract projection commit together.
            await _reportContracts.RebuildContractAsync(journeyId, ct);
        }, ct);

        return ServiceResult<SaveDetectionConfigResult>.Success(
            new SaveDetectionConfigResult(config, input.StageOverrides.Count, input.TouchpointOverrides.Count));
    }

    /// <summary>
    /// Returns the journey's saved detection config plus its full override set (stage- and
    /// touchpoint-scoped), or <c>null</c> when none exists — the API layer maps <c>null</c> to 404
    /// <c>journey.no_detection_config</c> (<c>contracts/configuration-api.md §GET detection</c>).
    /// </summary>
    public async Task<DetectionConfigView?> GetDetectionConfigAsync(Guid journeyId, CancellationToken ct = default)
    {
        var config = await _detection.GetByJourneyAsync(journeyId, ct);
        if (config is null)
        {
            return null;
        }

        var overrides = await _detection.ListOverridesAsync(config.DetectionConfigId, ct);
        return new DetectionConfigView(config, overrides);
    }

    private static bool InRange(decimal value) => value >= MinThreshold && value <= MaxThreshold;

    /// <summary>True when a non-null override threshold falls outside [0, 100] (null inherits, so it's in-range).</summary>
    private static bool OutOfRange(DetectionOverrideInput ov) =>
        (ov.PainThreshold is { } pain && !InRange(pain))
        || (ov.HappyThreshold is { } happy && !InRange(happy));

    /// <summary>
    /// Projects the input overrides into <see cref="DetectionThresholdOverride"/> rows FK'd to
    /// <paramref name="configId"/>. Stage overrides are listed first, then touchpoint overrides.
    /// </summary>
    private static List<DetectionThresholdOverride> BuildOverrides(
        Guid configId, SaveDetectionConfigInput input, DateTimeOffset now)
    {
        var overrides = new List<DetectionThresholdOverride>(
            input.StageOverrides.Count + input.TouchpointOverrides.Count);
        overrides.AddRange(input.StageOverrides.Select(o => MapOverride(configId, StageScope, o, now)));
        overrides.AddRange(input.TouchpointOverrides.Select(o => MapOverride(configId, TouchpointScope, o, now)));
        return overrides;
    }

    private static DetectionThresholdOverride MapOverride(
        Guid configId, string scopeType, DetectionOverrideInput ov, DateTimeOffset now) => new()
        {
            OverrideId = Guid.NewGuid(),
            DetectionConfigId = configId,
            ScopeType = scopeType,
            ScopeId = ov.ScopeId,
            PainThreshold = ov.PainThreshold,
            HappyThreshold = ov.HappyThreshold,
            CreatedAt = now,
            UpdatedAt = now,
        };
}

/// <summary>
/// Outcome of a journey detection-config save (<c>PUT /api/v1/journeys/{id}/detection</c> 200 body).
/// </summary>
/// <param name="Config">The persisted journey-level detection config.</param>
/// <param name="StageOverrideCount">Number of per-stage overrides saved.</param>
/// <param name="TouchpointOverrideCount">Number of per-touchpoint overrides saved.</param>
public sealed record SaveDetectionConfigResult(
    DetectionConfig Config,
    int StageOverrideCount,
    int TouchpointOverrideCount);

/// <summary>
/// Read model for <c>GET /api/v1/journeys/{id}/detection</c>: the journey-level config plus its full
/// override set. The API layer splits <see cref="Overrides"/> by
/// <see cref="DetectionThresholdOverride.ScopeType"/> into the stage- and touchpoint-scoped lists.
/// </summary>
/// <param name="Config">The persisted journey-level detection config.</param>
/// <param name="Overrides">Every per-stage and per-touchpoint override for the config.</param>
public sealed record DetectionConfigView(
    DetectionConfig Config,
    IReadOnlyList<DetectionThresholdOverride> Overrides);
