using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.Platform.Contracts.M16;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Detection;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Journey detection-configuration and report-contract endpoints (T090 / US-4). Three operations
/// across two contracts:
/// <list type="bullet">
///   <item><description><c>PUT /api/v1/journeys/{id}/detection</c> — full-replace save of the journey's
///     pain/happy thresholds + per-stage/per-touchpoint overrides (<c>contracts/configuration-api.md</c>)</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/detection</c> — read the config + overrides; 404
///     <c>journey.no_detection_config</c> when none</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/reports</c> — the M-07 report contract for the journey
///     (<c>contracts/journeys-api.md</c>); 404 <c>journey.no_report_contract</c> when none</description></item>
/// </list>
/// The save delegates to <see cref="DetectionConfigService"/> (validate → upsert config + full-replace
/// overrides + <c>journey.detection_config.updated</c> event + report-contract rebuild, one tx); the
/// reports read goes through the published <see cref="IReportContractReader"/> (same in-process read
/// M-07 uses). Authentication is enforced by <c>[Authorize]</c> (missing/invalid session → 401);
/// authorization (the <c>journey.read</c>/<c>journey.write</c> permissions) is deferred to the M-10
/// integration, like the sibling controllers. All responses follow the API-05 error envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/journeys")]
public sealed class DetectionController : ControllerBase
{
    private const string StageScope = "stage";
    private const string TouchpointScope = "touchpoint";

    private readonly DetectionConfigService _detection;
    private readonly IReportContractReader _reportContracts;
    private readonly ISessionContextAccessor _sessionAccessor;

    public DetectionController(
        DetectionConfigService detection,
        IReportContractReader reportContracts,
        ISessionContextAccessor sessionAccessor)
    {
        _detection = detection;
        _reportContracts = reportContracts;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// PUT /api/v1/journeys/{id}/detection — Full-replace save of the journey's detection
    /// configuration. The request body is the complete, authoritative pain/happy config; the service
    /// validates it (all-or-nothing), then upserts the config, full-replaces its overrides, publishes
    /// journey.detection_config.updated, and rebuilds the report contract — all in one transaction.
    /// Required permission: journey.write.
    /// </summary>
    [HttpPut("{id}/detection")]
    [ProducesResponseType(typeof(SaveDetectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SaveDetectionResponse>> SaveDetection(
        [FromRoute] Guid id,
        [FromBody] SaveDetectionRequestDto request,
        CancellationToken ct = default)
    {
        var session = _sessionAccessor.Current!;

        var actor = new ActorContext(
            session.UserId,
            session.Persona,
            HttpContext.CorrelationId());

        var input = new SaveDetectionConfigInput(
            request.PainThreshold,
            request.HappyThreshold,
            (request.StageOverrides ?? [])
                .Select(o => new DetectionOverrideInput(o.StageId, o.PainThreshold, o.HappyThreshold))
                .ToList(),
            (request.TouchpointOverrides ?? [])
                .Select(o => new DetectionOverrideInput(o.TouchpointId, o.PainThreshold, o.HappyThreshold))
                .ToList());

        var result = await _detection.SaveDetectionConfigAsync(id, input, actor, ct);
        if (!result.IsSuccess)
        {
            // Every detection save error (threshold_invalid / out_of_range / unknown_stage /
            // unknown_touchpoint) is a 422 per contracts/configuration-api.md.
            return UnprocessableEntity(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = result.Error!.Code,
                    Message = result.Error.Message
                }
            });
        }

        var saved = result.Value!;
        return Ok(new SaveDetectionResponse
        {
            JourneyId = saved.Config.JourneyId,
            PainThreshold = saved.Config.PainThreshold,
            HappyThreshold = saved.Config.HappyThreshold,
            StageOverrideCount = saved.StageOverrideCount,
            TouchpointOverrideCount = saved.TouchpointOverrideCount,
            UpdatedAt = saved.Config.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id}/detection — Returns the journey's detection configuration with all
    /// overrides, split into stage- and touchpoint-scoped lists. Returns 404
    /// journey.no_detection_config when none has been saved. Required permission: journey.read.
    /// </summary>
    [HttpGet("{id}/detection")]
    [ProducesResponseType(typeof(DetectionConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DetectionConfigResponse>> GetDetection(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var view = await _detection.GetDetectionConfigAsync(id, ct);
        if (view is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = "journey.no_detection_config",
                    Message = "No detection configuration has been saved for this journey."
                }
            });
        }

        var stageOverrides = view.Overrides
            .Where(o => string.Equals(o.ScopeType, StageScope, StringComparison.OrdinalIgnoreCase))
            .Select(o => new StageOverrideDto(o.ScopeId, o.PainThreshold, o.HappyThreshold))
            .ToList();

        var touchpointOverrides = view.Overrides
            .Where(o => string.Equals(o.ScopeType, TouchpointScope, StringComparison.OrdinalIgnoreCase))
            .Select(o => new TouchpointOverrideDto(o.ScopeId, o.PainThreshold, o.HappyThreshold))
            .ToList();

        return Ok(new DetectionConfigResponse
        {
            JourneyId = view.Config.JourneyId,
            PainThreshold = view.Config.PainThreshold,
            HappyThreshold = view.Config.HappyThreshold,
            StageOverrides = stageOverrides,
            TouchpointOverrides = touchpointOverrides,
            UpdatedAt = view.Config.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id}/reports — Returns the M-07 report contract for the journey (the same
    /// pre-built projection IReportContractReader serves in-process). Returns 404
    /// journey.no_report_contract when the journey has no stages / no contract generated yet.
    /// Required permission: journey.read.
    /// </summary>
    [HttpGet("{id}/reports")]
    [ProducesResponseType(typeof(ReportContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportContractDto>> GetReportContract(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var contract = await _reportContracts.GetReportContractAsync(id, ct);
        if (contract is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = "journey.no_report_contract",
                    Message = "This journey has no report contract — it has no stages or none has been generated yet."
                }
            });
        }

        return Ok(contract);
    }
}

/// <summary>
/// Request body for <c>PUT /api/v1/journeys/{id}/detection</c>. The override lists are the complete
/// desired state (full replace), not a delta; a null list is treated as empty. A null threshold in an
/// override means "inherit from the parent level".
/// </summary>
public sealed record SaveDetectionRequestDto(
    decimal PainThreshold,
    decimal HappyThreshold,
    IReadOnlyList<StageOverrideDto>? StageOverrides,
    IReadOnlyList<TouchpointOverrideDto>? TouchpointOverrides);

/// <summary>A per-stage detection override (request and GET response). Null threshold = inherit.</summary>
public sealed record StageOverrideDto(Guid StageId, decimal? PainThreshold, decimal? HappyThreshold);

/// <summary>A per-touchpoint detection override (request and GET response). Null threshold = inherit.</summary>
public sealed record TouchpointOverrideDto(Guid TouchpointId, decimal? PainThreshold, decimal? HappyThreshold);

/// <summary>200 body for <c>PUT /api/v1/journeys/{id}/detection</c> (counts only, per contract).</summary>
public sealed record SaveDetectionResponse
{
    public Guid JourneyId { get; init; }
    public decimal PainThreshold { get; init; }
    public decimal HappyThreshold { get; init; }
    public int StageOverrideCount { get; init; }
    public int TouchpointOverrideCount { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>200 body for <c>GET /api/v1/journeys/{id}/detection</c>, echoing the config + all overrides.</summary>
public sealed record DetectionConfigResponse
{
    public Guid JourneyId { get; init; }
    public decimal PainThreshold { get; init; }
    public decimal HappyThreshold { get; init; }
    public IReadOnlyList<StageOverrideDto> StageOverrides { get; init; } = [];
    public IReadOnlyList<TouchpointOverrideDto> TouchpointOverrides { get; init; } = [];
    public DateTime UpdatedAt { get; init; }
}
