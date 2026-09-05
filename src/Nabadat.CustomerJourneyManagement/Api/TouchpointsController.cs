using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.KpiBindings;
using Nabadat.CustomerJourneyManagement.Application.Touchpoints;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Touchpoint mutation endpoints. Implements four touchpoint operations across two contracts:
/// <list type="bullet">
///   <item><description><c>POST /api/v1/stages/{stageId}/touchpoints</c> — add a touchpoint to a stage (T030/US-1, <c>contracts/journeys-api.md</c>, <c>journey.write</c>)</description></item>
///   <item><description><c>PUT /api/v1/touchpoints/{touchpointId}</c> — update touchpoint metadata (T030/US-1, <c>journey.write</c>)</description></item>
///   <item><description><c>DELETE /api/v1/touchpoints/{touchpointId}</c> — delete a touchpoint and its KPI bindings (T030/US-1, <c>journey.write</c>)</description></item>
///   <item><description><c>PUT /api/v1/touchpoints/{touchpointId}/kpis</c> — full-replace the touchpoint's KPI bindings (T050/US-2, <c>contracts/configuration-api.md</c>, <c>journey.write</c>)</description></item>
/// </list>
/// The operations span two route bases (<c>/stages/{id}/touchpoints</c> for add, <c>/touchpoints/{id}</c>
/// for update/delete/kpis), so route templates are declared per action rather than on the controller.
/// The KPI-binding save delegates to <see cref="KpiBindingService"/> (validate → atomic full replace +
/// M-17 event + report-contract rebuild) and returns the persisted set with the non-blocking
/// <c>npsWarning</c> flag (true when NPS is bound) and each binding's resolved <c>scoringDirection</c>.
/// The tenant is resolved from the JWT (API-02) by <c>M10AuthenticationMiddleware</c>; every non-2xx
/// response follows the API-05 error envelope, mapped from each service failure code in
/// <see cref="MapError"/>. JSON property names follow the camelCase policy — note
/// <see cref="AddTouchpointRequestDto.IsMoT"/> serialises to <c>isMoT</c>, matching the contract and
/// the journey-tree DTO in <see cref="JourneysController"/>.
/// </summary>
[ApiController]
[Authorize]
public sealed class TouchpointsController : ControllerBase
{
    private readonly TouchpointService _touchpoints;
    private readonly KpiBindingService _kpiBindings;
    private readonly IKpiTypeDataService _kpiTypes;
    private readonly ISessionContextAccessor _sessionAccessor;

    public TouchpointsController(
        TouchpointService touchpoints,
        KpiBindingService kpiBindings,
        IKpiTypeDataService kpiTypes,
        ISessionContextAccessor sessionAccessor)
    {
        _touchpoints = touchpoints;
        _kpiBindings = kpiBindings;
        _kpiTypes = kpiTypes;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// POST /api/v1/stages/{stageId}/touchpoints — Adds a touchpoint to a stage. Fails with
    /// journey.touchpoint_limit_reached (422) when the tenant per-stage limit is exceeded and
    /// journey.archived_immutable (403) when the parent journey is Archived. Required permission:
    /// journey.write.
    /// </summary>
    [HttpPost("api/v1/stages/{stageId:guid}/touchpoints")]
    [ProducesResponseType(typeof(AddTouchpointResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddTouchpointResponse>> AddTouchpoint(
        [FromRoute] Guid stageId,
        [FromBody] AddTouchpointRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new AddTouchpointRequest(
            request.Name,
            request.Description,
            request.Channels,
            string.IsNullOrWhiteSpace(request.Importance) ? "Medium" : request.Importance,
            request.IsMoT,
            request.IsMandatory);

        var result = await _touchpoints.AddTouchpointAsync(stageId, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var touchpoint = result.Value!;
        return StatusCode(
            StatusCodes.Status201Created,
            new AddTouchpointResponse
            {
                TouchpointId = touchpoint.TouchpointId,
                CreatedAt = touchpoint.CreatedAt.UtcDateTime
            });
    }

    /// <summary>
    /// PUT /api/v1/touchpoints/{touchpointId} — Updates touchpoint metadata. Required permission:
    /// journey.write.
    /// </summary>
    [HttpPut("api/v1/touchpoints/{touchpointId:guid}")]
    [ProducesResponseType(typeof(UpdateTouchpointResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UpdateTouchpointResponse>> UpdateTouchpoint(
        [FromRoute] Guid touchpointId,
        [FromBody] UpdateTouchpointRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new UpdateTouchpointRequest(
            request.Name,
            request.Description,
            request.Channels,
            string.IsNullOrWhiteSpace(request.Importance) ? "Medium" : request.Importance,
            request.IsMoT,
            request.IsMandatory);

        var result = await _touchpoints.UpdateTouchpointAsync(touchpointId, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var touchpoint = result.Value!;
        return Ok(new UpdateTouchpointResponse
        {
            TouchpointId = touchpoint.TouchpointId,
            UpdatedAt = touchpoint.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// DELETE /api/v1/touchpoints/{touchpointId} — Deletes a touchpoint and its KPI bindings.
    /// Required permission: journey.write.
    /// </summary>
    [HttpDelete("api/v1/touchpoints/{touchpointId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTouchpoint(
        [FromRoute] Guid touchpointId,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var result = await _touchpoints.DeleteTouchpointAsync(touchpointId, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    /// <summary>
    /// PUT /api/v1/touchpoints/{touchpointId}/kpis — Full-replaces the touchpoint's KPI bindings
    /// (<c>contracts/configuration-api.md</c>). The body is the complete authoritative set; a null or
    /// empty list saves an unmeasured touchpoint (all existing bindings deleted). Weights are validated
    /// (each in (0,100], no duplicate type, known type, sum = 100) before any write; on success the
    /// binding replace, the journey.kpi_bindings.updated event, and the report-contract rebuild commit
    /// in one transaction. The 200 body carries the persisted set, isMeasured, the non-blocking
    /// npsWarning flag, and each binding's resolved scoringDirection. Fails with
    /// journey.archived_immutable (403) when the parent journey is Archived and a kpi.* code (422) on a
    /// weight-rule violation. Required permission: journey.write.
    /// </summary>
    [HttpPut("api/v1/touchpoints/{touchpointId:guid}/kpis")]
    [ProducesResponseType(typeof(SaveKpiBindingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<SaveKpiBindingsResponse>> SaveKpiBindings(
        [FromRoute] Guid touchpointId,
        [FromBody] SaveKpiBindingsRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        // A null/absent kpiBindings array is treated as the empty set (unmeasured touchpoint).
        var inputs = (request.KpiBindings ?? [])
            .Select(binding => new KpiBindingInput(binding.KpiType, binding.Weight))
            .ToList();

        var result = await _kpiBindings.SaveKpiBindingsAsync(touchpointId, inputs, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var saved = result.Value!;
        return Ok(new SaveKpiBindingsResponse
        {
            TouchpointId = saved.TouchpointId,
            KpiBindings = await BuildBindingResponsesAsync(saved.KpiBindings, ct),
            IsMeasured = saved.IsMeasured,
            NpsWarning = saved.NpsWarning,
            UpdatedAt = saved.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// Projects the persisted bindings to wire DTOs, resolving each binding's <c>scoringDirection</c>
    /// (which <c>kpi_bindings</c> does not store): platform-standard KPIs derive it intrinsically — all
    /// <see cref="ScoringDirection.Ascending"/> except <c>CES</c>, which is
    /// <see cref="ScoringDirection.Descending"/> — while tenant-defined KPIs read it from their
    /// <c>kpi_type_definitions</c> row (looked up once per distinct key). Mirrors the resolution in
    /// <see cref="Application.Scoring.JourneyConfigReaderService"/> that M-06 consumes.
    /// </summary>
    private async Task<IReadOnlyList<KpiBindingResponseDto>> BuildBindingResponsesAsync(
        IReadOnlyList<KpiBinding> bindings,
        CancellationToken ct)
    {
        var tenantDirections = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (binding.IsPlatformStandard || tenantDirections.ContainsKey(binding.KpiType))
            {
                continue;
            }

            var definition = await _kpiTypes.GetByKeyAsync(binding.KpiType, ct);
            tenantDirections[binding.KpiType] = definition?.ScoringDirection ?? nameof(ScoringDirection.Ascending);
        }

        return bindings
            .Select(binding => new KpiBindingResponseDto
            {
                KpiBindingId = binding.KpiBindingId,
                KpiType = binding.KpiType,
                Weight = binding.Weight,
                IsPlatformStandard = binding.IsPlatformStandard,
                ScoringDirection = ResolveScoringDirection(binding, tenantDirections)
            })
            .ToList();
    }

    /// <summary>
    /// Resolves a single binding's scoring direction to its wire string. Platform-standard: <c>CES</c>
    /// is Descending (lower effort is better), every other built-in is Ascending. Tenant-defined: the
    /// value cached from <c>kpi_type_definitions</c>, normalised through the enum (unknown ⇒ Ascending).
    /// </summary>
    private static string ResolveScoringDirection(
        KpiBinding binding,
        IReadOnlyDictionary<string, string> tenantDirections)
    {
        if (binding.IsPlatformStandard)
        {
            return string.Equals(binding.KpiType, nameof(PlatformKpiType.CES), StringComparison.Ordinal)
                ? nameof(ScoringDirection.Descending)
                : nameof(ScoringDirection.Ascending);
        }

        var stored = tenantDirections.GetValueOrDefault(binding.KpiType);
        return Enum.TryParse<ScoringDirection>(stored, ignoreCase: true, out var direction)
            ? direction.ToString()
            : nameof(ScoringDirection.Ascending);
    }

    /// <summary>
    /// Resolves the authenticated caller into an <see cref="ActorContext"/>. The session is
    /// guaranteed present by the controller's <c>[Authorize]</c> gate.
    /// </summary>
    private ActorContext CurrentActor()
    {
        var session = _sessionAccessor.Current!;
        return new ActorContext(session.UserId, session.Persona, HttpContext.CorrelationId());
    }

    /// <summary>
    /// Maps a <see cref="TouchpointService"/> failure <see cref="Error"/> onto the HTTP status defined
    /// for it in <c>contracts/journeys-api.md</c>, wrapped in the API-05 envelope. Unknown codes
    /// default to 422 (validation) — this covers both <c>journey.validation_error</c> and
    /// <c>journey.touchpoint_limit_reached</c>, which the contract maps to 422.
    /// </summary>
    private ObjectResult MapError(Error error) => error.Code switch
    {
        "journey.not_found" => NotFound(Envelope(error)),
        "journey.stage_not_found" => NotFound(Envelope(error)),
        "journey.touchpoint_not_found" => NotFound(Envelope(error)),
        "journey.archived_immutable" => StatusCode(StatusCodes.Status403Forbidden, Envelope(error)),
        _ => UnprocessableEntity(Envelope(error))
    };

    /// <summary>Wraps an <see cref="Error"/> in the API-05 response envelope.</summary>
    private static ApiErrorResponse Envelope(Error error) => new()
    {
        Error = new ApiErrorDetail { Code = error.Code, Message = error.Message }
    };
}

/// <summary>API request/response DTOs for touchpoint endpoints.</summary>

/// <summary>
/// Add-touchpoint request body (<c>POST /api/v1/stages/{stageId}/touchpoints</c>). Only
/// <see cref="Name"/> is required; <see cref="IsMoT"/> serialises to/from <c>isMoT</c> under the
/// camelCase policy, matching the contract.
/// </summary>
public sealed record AddTouchpointRequestDto(
    string Name,
    string? Description = null,
    string[]? Channels = null,
    string Importance = "Medium",
    bool IsMoT = false,
    bool IsMandatory = false);

/// <summary>Update-touchpoint request body (<c>PUT /api/v1/touchpoints/{touchpointId}</c>); same shape as add.</summary>
public sealed record UpdateTouchpointRequestDto(
    string Name,
    string? Description = null,
    string[]? Channels = null,
    string Importance = "Medium",
    bool IsMoT = false,
    bool IsMandatory = false);

public sealed record AddTouchpointResponse
{
    public Guid TouchpointId { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record UpdateTouchpointResponse
{
    public Guid TouchpointId { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// KPI-binding full-replace request body (<c>PUT /api/v1/touchpoints/{touchpointId}/kpis</c>). The
/// <see cref="KpiBindings"/> list is the complete authoritative set; <see langword="null"/> or an empty
/// list saves an unmeasured touchpoint (all existing bindings deleted).
/// </summary>
public sealed record SaveKpiBindingsRequestDto(IReadOnlyList<KpiBindingRequestItem>? KpiBindings);

/// <summary>
/// One requested KPI binding. <see cref="Weight"/> is <see langword="decimal"/> (numeric(5,2)) so
/// fractional weights sum to exactly 100 without IEEE-754 drift.
/// </summary>
public sealed record KpiBindingRequestItem(string KpiType, decimal Weight);

/// <summary>
/// KPI-binding full-replace 200 response. <see cref="IsMeasured"/> is true when the set is non-empty;
/// <see cref="NpsWarning"/> is a non-blocking flag (true when <c>NPS</c> is bound) the UI surfaces as a
/// survey-distribution reminder.
/// </summary>
public sealed record SaveKpiBindingsResponse
{
    public Guid TouchpointId { get; init; }
    public IReadOnlyList<KpiBindingResponseDto> KpiBindings { get; init; } = [];
    public bool IsMeasured { get; init; }
    public bool NpsWarning { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>One persisted KPI binding in the save response, including its resolved scoring direction.</summary>
public sealed record KpiBindingResponseDto
{
    public Guid KpiBindingId { get; init; }
    public string KpiType { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public bool IsPlatformStandard { get; init; }
    public string ScoringDirection { get; init; } = "Ascending";
}
