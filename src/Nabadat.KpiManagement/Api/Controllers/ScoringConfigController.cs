using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.KpiManagement.Api.Contracts;
using Nabadat.KpiManagement.Application.ScoringConfig;
using Nabadat.Platform.Contracts.M16;
using Nabadat.UserManagement.Api.Authorization;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.KpiManagement.Api.Controllers;

/// <summary>
/// Tenant Customer Journey ScoringConfig endpoints (contracts/settings-api.md, US-4):
/// <list type="bullet">
///   <item><c>GET /api/v1/tenant/scoring-config</c> — the five tenant parameters + derived β (P-01 / P-07).</item>
///   <item><c>PUT /api/v1/tenant/scoring-config</c> — update them (<b>P-01 only</b>; P-07 is read-only).</item>
/// </list>
/// The config is M-16-owned (the <c>scoring_configs</c> singleton): writes delegate to M-06's
/// <see cref="ScoringConfigUpdateService"/> which validates then calls M-16's published
/// <see cref="IScoringConfigStore"/> — the row, the <c>journey.scoring_config.updated</c> event, and
/// the transaction all live on M-16's side (AD-01). Reads need <c>View</c> on the
/// <c>TenantConfiguration</c> module; writes need <c>Manage</c> AND persona P-01 (FR-062). Every
/// non-2xx uses the API-05 envelope.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tenant/scoring-config")]
public sealed class ScoringConfigController : ControllerBase
{
    private const string ProgramManagerPersona = "P-01";

    private readonly ScoringConfigUpdateService _service;
    private readonly ISessionContextAccessor _session;

    public ScoringConfigController(ScoringConfigUpdateService service, ISessionContextAccessor session)
    {
        _service = service;
        _session = session;
    }

    /// <summary>GET /api/v1/tenant/scoring-config — current parameters (seeded defaults on a fresh tenant).</summary>
    [HttpGet]
    [RequirePermission(PermissionModule.TenantConfiguration, PermissionMode.View)]
    [ProducesResponseType(typeof(ScoringConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetScoringConfig(CancellationToken ct = default) =>
        Ok(ToResponse(await _service.GetAsync(ct)));

    /// <summary>PUT /api/v1/tenant/scoring-config — update the five parameters (P-01 only).</summary>
    [HttpPut]
    [RequirePermission(PermissionModule.TenantConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(ScoringConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateScoringConfig(
        [FromBody] ScoringConfigUpdateRequest request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        // FR-062: only the CX Program Manager (P-01) may write. P-07 holds Manage on TenantConfiguration
        // (it can edit Organization) but is read-only for ScoringConfig, so the Manage gate alone is not enough.
        if (!string.Equals(actor.Persona, ProgramManagerPersona, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                Envelope("PERMISSION_DENIED", "Only the CX Program Manager (P-01) can edit the scoring configuration."));
        }

        var input = new ScoringConfigInput(
            request.Alpha, request.MotMultiplier, request.NFloor, request.FlagPercentile, request.RollingWindowDays);

        var result = await _service.UpdateAsync(
            input, new ScoringConfigActor(actor.UserId, actor.Persona, Correlation()), ct);

        return result.Status == ScoringConfigSaveStatus.Failed
            ? MapValidationError(result.ErrorCode)
            : Ok(ToResponse(result.Config!));
    }

    // ----- helpers ---------------------------------------------------------------------------

    private static ScoringConfigResponse ToResponse(ScoringConfigDto dto) => new()
    {
        Alpha = dto.Alpha,
        Beta = AlphaBetaDeriver.Beta(dto.Alpha),
        MotMultiplier = dto.MotMultiplier,
        NFloor = dto.NFloor,
        FlagPercentile = dto.FlagPercentile,
        RollingWindowDays = dto.RollingWindowDays,
        Audit = new ScoringConfigAuditDto { UpdatedAt = dto.UpdatedAt, UpdatedBy = dto.UpdatedBy },
    };

    /// <summary>Maps the domain validation code (M-06 validator or the M-16 store backstop) to the API-05 wire code.</summary>
    private IActionResult MapValidationError(string? code) => code switch
    {
        ScoringConfigValidator.AlphaOutOfRangeCode or "scoring.alpha_out_of_range" =>
            BadRequest(Envelope("INVALID_ALPHA_BETA_SUM", "Alpha must be between 0.000 and 1.000.")),
        ScoringConfigValidator.MotOutOfRangeCode or "scoring.mot_multiplier_out_of_range" =>
            BadRequest(Envelope("MOT_MULTIPLIER_OUT_OF_RANGE", "MOT multiplier must be between 1.0 and 2.0.")),
        ScoringConfigValidator.NFloorBelowMinimumCode or "scoring.n_floor_below_minimum" =>
            BadRequest(Envelope("N_FLOOR_BELOW_MINIMUM", "Responses count floor must be at least 1.")),
        ScoringConfigValidator.FlagPercentileOutOfRangeCode or "scoring.flag_percentile_out_of_range" =>
            BadRequest(Envelope("FLAG_PERCENTILE_OUT_OF_RANGE", "Flag percentile must be between 1 and 49.")),
        ScoringConfigValidator.RollingWindowBelowMinimumCode or "scoring.rolling_window_below_minimum" =>
            BadRequest(Envelope("ROLLING_WINDOW_BELOW_MINIMUM", "Rolling window must be at least 7 days.")),
        _ => BadRequest(Envelope("SCORING_CONFIG_VALIDATION_FAILED", "The scoring configuration is invalid.")),
    };

    private (Guid UserId, string Persona) CurrentActor()
    {
        var session = _session.Current;
        return session is null ? (Guid.Empty, ProgramManagerPersona) : (session.UserId, session.Persona);
    }

    private Guid Correlation() =>
        Guid.TryParse(HttpContext.TraceIdentifier, out var g) ? g : Guid.NewGuid();

    private ApiErrorEnvelope Envelope(string code, string message) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = Correlation().ToString(),
        },
    };
}
