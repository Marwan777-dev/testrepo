using System.Text.Json;

namespace Nabadat.CustomerJourneyManagement.Application.Versioning;

/// <summary>
/// Captures a journey's full configuration tree as a single self-contained JSON blob at publish
/// time (T066 / US-3, <c>research.md §1</c>). The returned string is stored verbatim in
/// <c>journey_versions.snapshot_payload</c> (a <c>jsonb</c> column) and never updated, so a
/// published version is an immutable historical record.
/// <para>
/// Producing a <i>string</i> is the deep copy: once <see cref="Serialize"/> has materialised the
/// payload, later mutations to the live entities it was built from cannot change the captured
/// blob — the property that makes a version frozen. Pure logic, no I/O, no database; the
/// tenant-schema read that assembles the <see cref="JourneySnapshotInput"/> lives behind
/// <see cref="IJourneySnapshotBuilder"/> and is integration-tested separately.
/// </para>
/// </summary>
public sealed class JourneySnapshotSerializer
{
    // camelCase property names, matching the snapshot shape documented in research.md §1.
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serializes <paramref name="input"/> to the self-contained snapshot JSON. Captures the
    /// journey root, the tenant scoring parameters active at publish (α/β/MOT/n_floor/flag_percentile/
    /// rolling_window_days — SRS §4.2.9), the detection config, and the full stage → touchpoint →
    /// KPI-binding tree (including touchpoint channels/importance/flags).
    /// </summary>
    public string Serialize(JourneySnapshotInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Tenant-level scoring parameters captured at publish time (SRS §4.2.9 / §11.7) so historical
        // recomputation uses the values that were live for this version. β is derived (1 − α).
        object? scoringConfig = input.ScoringConfig is null
            ? null
            : new
            {
                alpha = input.ScoringConfig.Alpha,
                beta = 1.000m - input.ScoringConfig.Alpha,
                motMultiplier = input.ScoringConfig.MotMultiplier,
                nFloor = input.ScoringConfig.NFloor,
                flagPercentile = input.ScoringConfig.FlagPercentile,
                rollingWindowDays = input.ScoringConfig.RollingWindowDays,
            };

        object? detectionConfig = input.DetectionConfig is null
            ? null
            : new
            {
                painThreshold = input.DetectionConfig.PainThreshold,
                happyThreshold = input.DetectionConfig.HappyThreshold,
            };

        var stages = input.Stages.Select(s => new
        {
            stageId = s.Stage.StageId,
            sequenceNumber = s.Stage.SequenceNumber,
            name = s.Stage.Name,
            description = s.Stage.Description,
            customerGoal = s.Stage.CustomerGoal,
            expectedEmotion = s.Stage.ExpectedEmotion,
            durationHint = s.Stage.DurationHint,
            touchpoints = s.Touchpoints.Select(t => new
            {
                touchpointId = t.Touchpoint.TouchpointId,
                name = t.Touchpoint.Name,
                description = t.Touchpoint.Description,
                channels = t.Touchpoint.Channels,
                importance = t.Touchpoint.Importance,
                isMot = t.Touchpoint.IsMot,
                isMandatory = t.Touchpoint.IsMandatory,
                kpiBindings = t.KpiBindings.Select(k => new
                {
                    type = k.KpiType,
                    weight = k.Weight,
                    isPlatformStandard = k.IsPlatformStandard,
                }).ToArray(),
            }).ToArray(),
        }).ToArray();

        var snapshot = new
        {
            journeyId = input.Journey.JourneyId,
            name = input.Journey.Name,
            description = input.Journey.Description,
            type = input.Journey.JourneyType,
            status = input.Journey.Status,
            scoringConfig,
            detectionConfig,
            stages,
        };

        return JsonSerializer.Serialize(snapshot, Options);
    }
}
