using System.Text.Json;
using Nabadat.Platform.Contracts.M16;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Reports;

/// <summary>
/// Rebuilds the M-07 report contract (<c>report_contracts.contract_payload</c>) for a journey
/// whenever its configuration changes (T087 / US-4). The contract is a self-contained JSONB
/// projection of the journey tree — every stage and touchpoint, the KPI types behind each
/// measured touchpoint, the fixed Phase-1 score-dimension quad, and the journey's pain/happy
/// detection thresholds — that M-07 reads via <c>IReportContractReader</c> without touching M-16
/// tables.
///
/// This replaces the Phase-2 no-op stub (T014b). It is injected by <c>KpiBindingService</c>
/// (T047, US-2) and <c>DetectionConfigService</c> (T085, US-4); both call
/// <see cref="RebuildContractAsync"/> inside their own unit-of-work so the contract is rebuilt in
/// the SAME transaction as the configuration write that triggered it (FR-015). The tree is loaded
/// through the published <see cref="IJourneyConfigReader"/> — which already enumerates unmeasured
/// touchpoints with empty bindings — rather than by re-querying tables.
/// </summary>
public sealed class ReportContractService
{
    /// <summary>
    /// The fixed Phase-1 score-dimension quad exposed in every contract
    /// (<c>contracts/published-interfaces.md</c> rule 3). M-07 keys its report layout off these.
    /// </summary>
    private static readonly string[] Phase1ScoreDimensions =
        ["journey_score", "stage_score", "touchpoint_score", "kpi_score"];

    /// <summary>camelCase serialization; the payload is opaque JSON to M-16 and read back by M-07.</summary>
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web);

    private readonly IJourneyConfigReader _journeyConfig;
    private readonly IReportContractDataService _reportContracts;
    private readonly IDetectionDataService _detection;
    private readonly TimeProvider _time;

    public ReportContractService(
        IJourneyConfigReader journeyConfig,
        IReportContractDataService reportContracts,
        IDetectionDataService detection,
        TimeProvider time)
    {
        _journeyConfig = journeyConfig;
        _reportContracts = reportContracts;
        _detection = detection;
        _time = time;
    }

    /// <summary>
    /// Projects the journey's live configuration into a <see cref="ReportContractDto"/>. Returns
    /// <c>null</c> when the journey has no configuration (so a caller can no-op rather than persist an
    /// empty contract). Every touchpoint is enumerated; an unmeasured touchpoint surfaces with
    /// <c>IsMeasured = false</c> and no KPI types, so it is absent from the KPI dimension list (FR-008).
    /// </summary>
    public async Task<ReportContractDto?> BuildContractAsync(Guid journeyId, CancellationToken ct = default)
    {
        var config = await _journeyConfig.GetJourneyConfigAsync(journeyId, ct);
        if (config is null)
        {
            return null;
        }

        var detection = await _detection.GetByJourneyAsync(journeyId, ct);

        var stages = config.Stages
            .Select(stage => new StageReportDto(
                stage.StageId,
                stage.Name,
                stage.SequenceNumber,
                stage.Touchpoints
                    .Select(touchpoint => new TouchpointReportDto(
                        touchpoint.TouchpointId,
                        touchpoint.Name,
                        touchpoint.IsMoT,
                        // Unmeasured touchpoints contribute no KPI types (excluded from the KPI
                        // dimension list, FR-008); measured ones surface their bound KPI types.
                        touchpoint.IsMeasured
                            ? touchpoint.KpiBindings.Select(binding => binding.KpiType).ToList()
                            : new List<string>(),
                        touchpoint.IsMeasured))
                    .ToList()))
            .ToList();

        return new ReportContractDto(
            config.JourneyId,
            config.Name,
            _time.GetUtcNow().UtcDateTime,
            stages,
            Phase1ScoreDimensions,
            new DetectionConfigReportDto(detection?.PainThreshold, detection?.HappyThreshold));
    }

    /// <summary>
    /// Rebuilds the contract and UPSERTs it into <c>report_contracts</c>. It is invoked inside the
    /// caller's <c>ITenantDbContext.ExecuteAsync</c> (FR-015 — same transaction as the configuration
    /// write), so the upsert flushes on the ambient transaction. When the journey has no
    /// configuration the build yields <c>null</c> and the rebuild is a no-op.
    /// </summary>
    public async Task RebuildContractAsync(Guid journeyId, CancellationToken ct = default)
    {
        var contract = await BuildContractAsync(journeyId, ct);
        if (contract is null)
        {
            return;
        }

        var now = _time.GetUtcNow();
        var entity = new ReportContract
        {
            // Fresh id used only on first insert; the repo keeps report_contract_id and created_at
            // out of the DO UPDATE set, so they survive subsequent rebuilds (mirrors ScoringConfig).
            ReportContractId = Guid.NewGuid(),
            JourneyId = journeyId,
            ContractPayload = JsonSerializer.Serialize(contract, PayloadOptions),
            GeneratedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _reportContracts.UpsertAsync(entity, ct);
    }
}
