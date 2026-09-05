using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.KpiManagement.Api.Contracts;
using Nabadat.KpiManagement.Application.Catalogue;
using Nabadat.KpiManagement.Application.Cxi;
using Nabadat.KpiManagement.Application.Catalogue.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Dtos;
using Nabadat.KpiManagement.Application.Kpis.Interfaces;
using Nabadat.KpiManagement.Domain.Entities;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Nabadat.UserManagement.Api.Authorization;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.Api.Controllers;

/// <summary>
/// KPI catalogue + configuration endpoints (contracts/kpi-api.md):
/// <list type="bullet">
///   <item><c>GET /api/v1/kpis</c> — cursor-paginated catalogue (US-1).</item>
///   <item><c>GET /api/v1/kpis/{id}</c> — full configuration (US-2).</item>
///   <item><c>POST /api/v1/kpis</c> — create a custom KPI (US-2).</item>
///   <item><c>PUT /api/v1/kpis/{id}</c> — update a KPI, with the FR-017 scale-change confirmation
///   gate via <c>?confirm_structural_change=true</c> (US-2).</item>
///   <item><c>GET /api/v1/kpis/{id}/binding-usage</c> — M-16 binding-usage probe (US-2).</item>
/// </list>
/// There is deliberately NO <c>DELETE</c> route (FR-002).
/// <para>
/// Authentication is enforced by <c>[Authorize]</c> against the host's PortalSession scheme; the
/// actor (user id / persona) is read from <see cref="ISessionContextAccessor"/>. Permission gating
/// layers on top via <see cref="RequirePermission"/>: reads require
/// <c>KpiConfiguration:View</c>, writes require <c>KpiConfiguration:Manage</c>; a persona whose
/// session snapshot lacks the mode gets 403 <c>PERMISSION_DENIED</c>. Every non-2xx response uses
/// the shared API-05 envelope (<see cref="ApiErrorEnvelope"/>).
/// </para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/kpis")]
public sealed class KpisController : ControllerBase
{
    private readonly IKpiDefinitionService _kpiDefinitions;
    private readonly IKpiConfigReader _reader;
    private readonly KpiSaveService _saveService;
    private readonly KpiBindingUsageProbe _bindingProbe;
    private readonly CxiWeightUpdateService _cxiWeightUpdates;
    private readonly KpiActivationCommandHandler _activation;
    private readonly ISessionContextAccessor _session;
    private readonly TimeProvider _time;

    public KpisController(
        IKpiDefinitionService kpiDefinitions,
        IKpiConfigReader reader,
        KpiSaveService saveService,
        KpiBindingUsageProbe bindingProbe,
        CxiWeightUpdateService cxiWeightUpdates,
        KpiActivationCommandHandler activation,
        ISessionContextAccessor session,
        TimeProvider time)
    {
        _kpiDefinitions = kpiDefinitions;
        _reader = reader;
        _saveService = saveService;
        _bindingProbe = bindingProbe;
        _cxiWeightUpdates = cxiWeightUpdates;
        _activation = activation;
        _session = session;
        _time = time;
    }

    /// <summary>
    /// GET /api/v1/kpis — Returns the tenant's KPI catalogue, cursor-paginated. Filters: <c>type</c>
    /// (All/Standard/Custom, default All), <c>active_only</c> (default true), <c>search</c>
    /// (case-insensitive substring over short/full name). Required permission: <c>kpis:read</c>.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.View)]
    [ProducesResponseType(typeof(KpiListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListKpis(
        [FromQuery] string? type = null,
        [FromQuery(Name = "active_only")] bool activeOnly = true,
        [FromQuery] string? search = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        if (limit is < 1 or > 200)
        {
            limit = 50;
        }

        var page = await _kpiDefinitions.ListCatalogueAsync(ParseTypeFilter(type), activeOnly, search, cursor, limit, ct);

        return Ok(new KpiListResponse
        {
            Items = page.Items.Select(KpiListItemMapper.Map).Select(ToResponse).ToList(),
            NextCursor = page.NextCursor,
        });
    }

    /// <summary>
    /// GET /api/v1/kpis/{key} — Returns one KPI's full configuration. <paramref name="key"/> is either
    /// the KPI's GUID id or its (case-insensitive) Short Name, so the configuration page can use a
    /// human-readable URL (<c>/kpi-management/cxi</c>). Required permission: <c>kpis:read</c>.
    /// 404 <c>KPI_NOT_FOUND</c> when absent.
    /// </summary>
    [HttpGet("{key}")]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.View)]
    [ProducesResponseType(typeof(KpiConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetKpi([FromRoute] string key, CancellationToken ct = default)
    {
        var id = await ResolveKpiIdAsync(key, ct);
        if (id is null)
        {
            return NotFound(Envelope("KPI_NOT_FOUND", "KPI not found."));
        }

        var body = await BuildConfigResponseAsync(id.Value, ct);
        return body is null
            ? NotFound(Envelope("KPI_NOT_FOUND", "KPI not found."))
            : Ok(body);
    }

    /// <summary>Resolves a route key (GUID id or case-insensitive Short Name) to a KPI id; null if absent.</summary>
    private async Task<Guid?> ResolveKpiIdAsync(string key, CancellationToken ct)
    {
        if (Guid.TryParse(key, out var guid))
        {
            return guid;
        }

        var definition = await _kpiDefinitions.GetByShortNameAsync(key, ct);
        return definition?.Id;
    }

    /// <summary>
    /// POST /api/v1/kpis — Creates a custom KPI. Required permission: <c>kpis:create</c>.
    /// Validation / reservation / threshold failures map to the documented 400 codes.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(KpiConfigResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateKpi([FromBody] KpiConfigRequest request, CancellationToken ct = default)
    {
        var actor = CurrentActor();
        var now = _time.GetUtcNow();
        var id = Guid.NewGuid();

        var definition = new KpiDefinition
        {
            Id = id,
            ShortName = request.ShortName ?? string.Empty,
            FullName = request.FullName ?? string.Empty,
            KpiType = KpiType.Custom,
            IsComposite = false,
            CalculationMethod = request.CalculationMethod,
            TopNValue = request.TopNValue,
            Scale = request.Scale,
            MinScaleDescriptionEn = request.MinScaleDescription?.En,
            MinScaleDescriptionAr = request.MinScaleDescription?.Ar,
            MaxScaleDescriptionEn = request.MaxScaleDescription?.En,
            MaxScaleDescriptionAr = request.MaxScaleDescription?.Ar,
            RepresentationStyle = request.RepresentationStyle,
            EmojiSet = request.EmojiSet,
            Target = request.Target,
            IsActive = request.IsActive,
            ShowOnDashboard = request.IsActive && request.ShowOnDashboard,
            CreatedAt = now,
            CreatedBy = actor.UserId,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
        };

        var command = new KpiSaveCommand(
            KpiSaveMode.Create,
            definition,
            BuildThreshold(id, request.Thresholds),
            BuildPerspectives(request.Perspectives, now),
            actor.UserId,
            actor.Persona,
            Correlation());

        var result = await _saveService.SaveAsync(command, ct);
        if (!result.Succeeded)
        {
            return MapSaveError(result.ErrorCode);
        }

        var body = await BuildConfigResponseAsync(result.KpiId, ct);
        return CreatedAtAction(nameof(GetKpi), new { key = result.KpiId }, body);
    }

    /// <summary>
    /// PUT /api/v1/kpis/{id} — Updates a KPI. Required permission: <c>kpis:update</c>. A Scale change
    /// that affects existing M-16 bindings requires <c>?confirm_structural_change=true</c> (FR-017),
    /// else 409 <c>KPI_SCALE_CHANGE_AFFECTS_BINDINGS</c>.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(KpiConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ScaleChangeConflictResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateKpi(
        [FromRoute] Guid id,
        [FromBody] KpiConfigRequest request,
        [FromQuery(Name = "confirm_structural_change")] bool confirmStructuralChange = false,
        CancellationToken ct = default)
    {
        var existing = await _kpiDefinitions.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return NotFound(Envelope("KPI_NOT_FOUND", "KPI not found."));
        }

        // FR-017: a scale change against bound touchpoints needs explicit confirmation.
        if (existing.Scale != request.Scale && !confirmStructuralChange)
        {
            var (touchpoints, journeys) = await _bindingProbe.GetUsageAsync(id, ct);
            if (touchpoints > 0)
            {
                return Conflict(new ScaleChangeConflictResponse
                {
                    Error = Detail("KPI_SCALE_CHANGE_AFFECTS_BINDINGS",
                        "Changing the scale affects touchpoints already bound to this KPI. Re-submit with confirm_structural_change=true to proceed."),
                    AffectedTouchpoints = touchpoints,
                    AffectedJourneys = journeys,
                });
            }
        }

        var actor = CurrentActor();
        var now = _time.GetUtcNow();

        var definition = new KpiDefinition
        {
            Id = id,
            ShortName = request.ShortName ?? existing.ShortName,
            FullName = request.FullName ?? string.Empty,
            KpiType = existing.KpiType,
            IsComposite = existing.IsComposite,
            CalculationMethod = request.CalculationMethod,
            TopNValue = request.TopNValue,
            Scale = request.Scale,
            MinScaleDescriptionEn = request.MinScaleDescription?.En,
            MinScaleDescriptionAr = request.MinScaleDescription?.Ar,
            MaxScaleDescriptionEn = request.MaxScaleDescription?.En,
            MaxScaleDescriptionAr = request.MaxScaleDescription?.Ar,
            RepresentationStyle = request.RepresentationStyle,
            EmojiSet = request.EmojiSet,
            Target = request.Target,
            IsActive = request.IsActive,
            ShowOnDashboard = request.IsActive && request.ShowOnDashboard,
            CreatedAt = existing.CreatedAt,
            CreatedBy = existing.CreatedBy,
            UpdatedAt = now,
            UpdatedBy = actor.UserId,
        };

        var command = new KpiSaveCommand(
            KpiSaveMode.Edit,
            definition,
            BuildThreshold(id, request.Thresholds),
            BuildPerspectives(request.Perspectives, now),
            actor.UserId,
            actor.Persona,
            Correlation());

        var result = await _saveService.SaveAsync(command, ct);
        if (!result.Succeeded)
        {
            return MapSaveError(result.ErrorCode);
        }

        var body = await BuildConfigResponseAsync(result.KpiId, ct);
        return Ok(body);
    }

    /// <summary>
    /// GET /api/v1/kpis/{id}/binding-usage — Returns the M-16 binding-usage counts for the KPI (used
    /// to prefetch the deactivation/scale-change confirmation). Required permission: <c>kpis:read</c>.
    /// </summary>
    [HttpGet("{id:guid}/binding-usage")]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.View)]
    [ProducesResponseType(typeof(BindingUsageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBindingUsage([FromRoute] Guid id, CancellationToken ct = default)
    {
        var exists = await _kpiDefinitions.GetByIdAsync(id, ct);
        if (exists is null)
        {
            return NotFound(Envelope("KPI_NOT_FOUND", "KPI not found."));
        }

        var (touchpoints, journeys) = await _bindingProbe.GetUsageAsync(id, ct);
        return Ok(new BindingUsageResponse { TouchpointCount = touchpoints, JourneyCount = journeys });
    }

    /// <summary>
    /// PATCH /api/v1/kpis/{id}/activation — Activates or deactivates a KPI (FR-026). Required
    /// permission: <c>kpis:activate</c> (P-01). <c>active=true</c> is idempotent (200). Deactivating a
    /// KPI still bound to M-16 touchpoints without <c>confirm=true</c> returns 409
    /// <c>KPI_DEACTIVATION_REQUIRES_CONFIRMATION</c> with the binding-usage counts; a confirmed (or
    /// unbound) deactivation cascades — Show-on-Dashboard forced off, the KPI removed from every CXI it
    /// belonged to — and emits exactly one <c>settings.changed</c> event.
    /// </summary>
    [HttpPatch("{id:guid}/activation")]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(KpiConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(DeactivationConfirmationResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SetActivation(
        [FromRoute] Guid id,
        [FromBody] KpiActivationRequest request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();
        var command = new KpiActivationCommand(
            id, request.Active, request.Confirm, actor.UserId, actor.Persona, Correlation());

        var result = await _activation.HandleAsync(command, ct);

        switch (result.Outcome)
        {
            case KpiActivationOutcome.NotFound:
                return NotFound(Envelope("KPI_NOT_FOUND", "KPI not found."));

            case KpiActivationOutcome.RequiresConfirmation:
                return Conflict(new DeactivationConfirmationResponse
                {
                    Error = Detail("KPI_DEACTIVATION_REQUIRES_CONFIRMATION",
                        "This KPI is bound to active touchpoints. Re-submit with confirm=true to deactivate it and exclude it from future scoring."),
                    TouchpointCount = result.TouchpointCount,
                    JourneyCount = result.JourneyCount,
                });

            default:
                var body = await BuildConfigResponseAsync(id, ct);
                return body is null ? NotFound(Envelope("KPI_NOT_FOUND", "KPI not found.")) : Ok(body);
        }
    }

    /// <summary>
    /// PUT /api/v1/kpis/{cxiId}/weights — Full-replace the CXI composite's member weights (US-3).
    /// Required permission: <c>kpis:cxi_weights:update</c> (P-01 only). Only valid when the id resolves
    /// to a composite KPI. Validation failures map to the documented 400 codes; an unknown / non-composite
    /// id → 404 <c>CXI_NOT_FOUND</c>.
    /// </summary>
    [HttpPut("{cxiId:guid}/weights")]
    [RequirePermission(PermissionModule.KpiConfiguration, PermissionMode.Manage)]
    [ProducesResponseType(typeof(CxiWeightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCxiWeights(
        [FromRoute] Guid cxiId,
        [FromBody] CxiWeightsRequest request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();
        var inputs = (request.Weights ?? [])
            .Select(w => new CxiWeightInput(w.MemberKpiId, w.Weight))
            .ToList();

        var result = await _cxiWeightUpdates.ReplaceAsync(cxiId, inputs, actor.UserId, actor.Persona, Correlation(), ct);
        if (!result.Succeeded)
        {
            return MapCxiWeightError(result.ErrorCode);
        }

        var dto = await _reader.GetByIdAsync(cxiId, ct);
        var items = (dto?.CxiWeights ?? [])
            .Select(w => new CxiWeightResponse
            {
                MemberKpiId = w.MemberKpiId,
                MemberShortName = w.MemberShortName,
                Weight = w.Weight,
                EffectivePercentage = w.EffectivePercentage,
            })
            .ToList();

        return Ok(new CxiWeightsResponse { Weights = items });
    }

    // ----- helpers ---------------------------------------------------------------------------

    /// <summary>Maps a <see cref="CxiWeightUpdateResult.ErrorCode"/> onto its contract HTTP status + API-05 envelope.</summary>
    private IActionResult MapCxiWeightError(string? code) => code switch
    {
        CxiWeightUpdateService.CxiNotFoundCode => NotFound(Envelope(code, "CXI KPI not found.")),
        CxiWeightUpdateService.CannotIncludeItselfCode =>
            BadRequest(Envelope(code, "The CXI composite cannot include itself as a member.")),
        CxiWeightUpdateService.MemberNotActiveCode =>
            BadRequest(Envelope(code, "A referenced member KPI is not an active KPI.")),
        CxiWeightUpdateService.InsufficientMembersCode =>
            BadRequest(Envelope(code, "CXI requires at least two members with assigned weights.")),
        CxiWeightUpdateService.WeightInvalidCode =>
            BadRequest(Envelope(code, "Each CXI member weight must be a positive integer.")),
        _ => BadRequest(Envelope("CXI_WEIGHT_UPDATE_FAILED", "The CXI weights could not be updated.")),
    };

    private async Task<KpiConfigResponse?> BuildConfigResponseAsync(Guid id, CancellationToken ct)
    {
        var dto = await _reader.GetByIdAsync(id, ct);
        if (dto is null)
        {
            return null;
        }

        // The reader DTO carries the configuration; the entity carries the audit block.
        var entity = await _kpiDefinitions.GetByIdAsync(id, ct);
        return ToConfigResponse(dto, entity);
    }

    private static KpiThreshold BuildThreshold(Guid kpiId, KpiThresholdInputDto? thresholds) => new()
    {
        KpiId = kpiId,
        LowerBound = thresholds?.LowerBound ?? 0m,
        X = thresholds?.X ?? 0m,
        Y = thresholds?.Y ?? 0m,
        UpperBound = thresholds?.UpperBound ?? 100m,
    };

    private static IReadOnlyList<KpiPerspective> BuildPerspectives(
        IReadOnlyList<KpiPerspectiveInputDto>? perspectives,
        DateTimeOffset now) =>
        (perspectives ?? [])
            .Select(p => new KpiPerspective
            {
                Id = Guid.NewGuid(),
                Label = p.Label,
                DisplayOrder = p.DisplayOrder,
                CreatedAt = now,
            })
            .ToList();

    private static KpiConfigResponse ToConfigResponse(KpiDefinitionDto dto, KpiDefinition? entity) => new()
    {
        Id = dto.Id,
        ShortName = dto.ShortName,
        FullName = dto.FullName,
        KpiType = dto.KpiType.ToString(),
        IsComposite = dto.IsComposite,
        CalculationMethod = dto.CalculationMethod.ToString(),
        TopNValue = dto.TopNValue,
        Scale = dto.Scale?.ToString(),
        MinScaleDescription = ToBilingual(dto.MinScaleDescription),
        MaxScaleDescription = ToBilingual(dto.MaxScaleDescription),
        RepresentationStyle = dto.RepresentationStyle?.ToString(),
        EmojiSet = dto.EmojiSet?.ToString(),
        Target = dto.Target,
        IsActive = dto.IsActive,
        ShowOnDashboard = dto.ShowOnDashboard,
        Thresholds = new KpiThresholdResponse
        {
            LowerBound = dto.Thresholds.LowerBound,
            X = dto.Thresholds.X,
            Y = dto.Thresholds.Y,
            UpperBound = dto.Thresholds.UpperBound,
        },
        Perspectives = dto.Perspectives
            .Select(p => new KpiPerspectiveResponse { Id = p.Id, Label = p.Label, DisplayOrder = p.DisplayOrder })
            .ToList(),
        CxiWeights = dto.CxiWeights?
            .Select(w => new CxiWeightResponse
            {
                MemberKpiId = w.MemberKpiId,
                MemberShortName = w.MemberShortName,
                Weight = w.Weight,
                EffectivePercentage = w.EffectivePercentage,
            })
            .ToList(),
        Audit = entity is null ? null : new KpiAuditResponse
        {
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
        },
    };

    private static BilingualTextDto? ToBilingual(BilingualText? text) =>
        text is null ? null : new BilingualTextDto { En = text.En, Ar = text.Ar };

    /// <summary>Maps a <see cref="KpiSaveResult.ErrorCode"/> onto its contract HTTP status + API-05 envelope.</summary>
    private IActionResult MapSaveError(string? code) => code switch
    {
        "short_name.duplicate" => BadRequest(Envelope("KPI_SHORT_NAME_DUPLICATE", "A KPI with this Short Name already exists.")),
        "threshold.must_be_ascending" or "threshold.not_ascending" =>
            BadRequest(Envelope("KPI_THRESHOLD_NOT_ASCENDING", "Threshold band edges must be strictly ascending.")),
        "calculation_method.nps_standard_reserved_for_nps" or "calculation_method.weighted_composite_reserved_for_cxi" =>
            BadRequest(Envelope("KPI_CALCULATION_METHOD_RESERVED", "This calculation method is reserved for a standard KPI.")),
        KpiSaveService.ShortNameImmutableCode =>
            BadRequest(Envelope(KpiSaveService.ShortNameImmutableCode, "Short Name cannot be changed after creation.")),
        KpiSaveService.FieldImmutableForStandardCode =>
            BadRequest(Envelope(KpiSaveService.FieldImmutableForStandardCode, "Scale and calculation method are locked for standard KPIs.")),
        KpiSaveService.NotFoundCode => NotFound(Envelope("KPI_NOT_FOUND", "KPI not found.")),
        null => BadRequest(Envelope("KPI_VALIDATION_FAILED", "The KPI configuration is invalid.")),
        _ => BadRequest(ValidationEnvelope(code)),
    };

    // Unrecognised values fall back to All (the contract default) rather than 400.
    private static KpiTypeFilter ParseTypeFilter(string? type) =>
        type?.Trim().ToLowerInvariant() switch
        {
            "standard" => KpiTypeFilter.Standard,
            "custom" => KpiTypeFilter.Custom,
            _ => KpiTypeFilter.All,
        };

    private (Guid UserId, string Persona) CurrentActor()
    {
        var session = _session.Current;
        return session is null ? (Guid.Empty, "P-01") : (session.UserId, session.Persona);
    }

    private Guid Correlation() =>
        Guid.TryParse(HttpContext.TraceIdentifier, out var g) ? g : Guid.NewGuid();

    private ApiErrorEnvelope Envelope(string code, string message) => new() { Error = Detail(code, message) };

    private ApiErrorDetail Detail(string code, string message) => new()
    {
        Code = code,
        Message = message,
        CorrelationId = Correlation().ToString(),
    };

    private ApiErrorEnvelope ValidationEnvelope(string fieldCode) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = "KPI_VALIDATION_FAILED",
            Message = "The KPI configuration is invalid.",
            CorrelationId = Correlation().ToString(),
            Details = [new ApiErrorFieldDetail { Field = fieldCode.Split('.')[0], Code = fieldCode }],
        },
    };

    private static KpiListItemResponse ToResponse(KpiListItemDto dto) => new()
    {
        Id = dto.Id,
        ShortName = dto.ShortName,
        FullName = dto.FullName,
        KpiType = dto.KpiType,
        IsComposite = dto.IsComposite,
        Scale = dto.Scale,
        CalculationMethod = dto.CalculationMethod,
        CalculationMethodLabel = dto.CalculationMethodLabel,
        ScaleLabel = dto.ScaleLabel,
        Target = dto.Target,
        IsActive = dto.IsActive,
        ShowOnDashboard = dto.ShowOnDashboard,
        CreatedAt = dto.CreatedAt,
    };
}
