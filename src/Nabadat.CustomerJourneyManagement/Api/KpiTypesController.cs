using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes;
using Nabadat.CustomerJourneyManagement.Application.KpiTypes.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// KPI-type catalog endpoints (T052 / US-2) per <c>contracts/configuration-api.md</c>:
/// <list type="bullet">
///   <item><description><c>GET /api/v1/kpi-types</c> — the six platform-standard built-ins plus the
///   tenant's defined custom types (<c>journey.read</c>, P-01/P-02)</description></item>
///   <item><description><c>POST /api/v1/kpi-types</c> — create a tenant-defined custom KPI type
///   (<c>journey.admin</c>, P-01 only); 409 <c>kpi_type.key_conflict</c> on a duplicate key</description></item>
/// </list>
/// Both delegate to <see cref="KpiTypeService"/>. Authentication is enforced in-code on the mutating
/// POST (missing/invalid session → 401, via <c>[Authorize]</c>); the documented <c>journey.admin</c> /
/// P-01-only authorization is still deferred to the M-10 authorization integration — no authorization
/// POLICY is declared here yet. This mirrors the M-16 journey/touchpoint controllers. Every non-2xx
/// response follows the API-05 error envelope (<see cref="ApiErrorResponse"/>, declared in
/// <see cref="JourneysController"/>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/kpi-types")]
public sealed class KpiTypesController : ControllerBase
{
    private readonly KpiTypeService _kpiTypes;
    private readonly IActiveKpiCatalogReader _catalog;
    private readonly ISessionContextAccessor _sessionAccessor;

    public KpiTypesController(
        KpiTypeService kpiTypes,
        IActiveKpiCatalogReader catalog,
        ISessionContextAccessor sessionAccessor)
    {
        _kpiTypes = kpiTypes;
        _catalog = catalog;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// GET /api/v1/kpi-types — Returns the KPI types available to bind on a touchpoint. In the deployed
    /// host this is the tenant's active KPI-Management catalogue (M-06): its Standard KPIs surface as
    /// <c>platformStandardTypes</c> and its Custom KPIs as <c>tenantDefinedTypes</c> (carrying the M-06
    /// id). Composite KPIs are excluded — they are computed, not measured at a touchpoint. Required
    /// permission: journey.read.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(KpiTypesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<KpiTypesResponse>> ListKpiTypes(CancellationToken ct = default)
    {
        var catalog = await _catalog.GetActiveKpisAsync(ct);

        return Ok(new KpiTypesResponse
        {
            PlatformStandardTypes = catalog
                .Where(entry => entry.IsPlatformStandard)
                .Select(entry => new PlatformKpiTypeDto
                {
                    TypeKey = entry.Key,
                    LabelAr = entry.LabelAr,
                    LabelEn = entry.LabelEn,
                    ScoringDirection = entry.ScoringDirection
                })
                .ToList(),
            TenantDefinedTypes = catalog
                .Where(entry => !entry.IsPlatformStandard)
                .Select(entry => new TenantKpiTypeDto
                {
                    KpiTypeDefinitionId = entry.KpiId ?? Guid.Empty,
                    TypeKey = entry.Key,
                    LabelAr = entry.LabelAr,
                    LabelEn = entry.LabelEn,
                    ScoringDirection = entry.ScoringDirection
                })
                .ToList()
        });
    }

    /// <summary>
    /// POST /api/v1/kpi-types — Creates a tenant-defined custom KPI type. Fails with
    /// kpi_type.validation_error (422) on a bad typeKey format / missing label / unknown scoring
    /// direction, and kpi_type.key_conflict (409) when the key collides with a platform-standard or an
    /// existing tenant type. Required permission: journey.admin (P-01 only).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateKpiTypeResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateKpiTypeResponse>> CreateKpiType(
        [FromBody] CreateKpiTypeRequestDto request,
        CancellationToken ct = default)
    {
        var input = new CreateKpiTypeInput(
            request.TypeKey,
            request.LabelAr,
            request.LabelEn,
            request.ScoringDirection);

        var result = await _kpiTypes.CreateAsync(input, ct);
        if (!result.IsSuccess)
        {
            var envelope = new ApiErrorResponse
            {
                Error = new ApiErrorDetail { Code = result.Error!.Code, Message = result.Error.Message }
            };
            return result.Error.Code switch
            {
                "kpi_type.key_conflict" => Conflict(envelope),
                _ => UnprocessableEntity(envelope)
            };
        }

        var definition = result.Value!;
        return StatusCode(
            StatusCodes.Status201Created,
            new CreateKpiTypeResponse
            {
                KpiTypeDefinitionId = definition.KpiTypeDefinitionId,
                TypeKey = definition.TypeKey,
                CreatedAt = definition.CreatedAt.UtcDateTime
            });
    }
}

/// <summary>API request/response DTOs for the KPI-type catalog endpoints.</summary>

/// <summary>
/// Create-KPI-type request body (<c>POST /api/v1/kpi-types</c>). <see cref="ScoringDirection"/> is
/// optional (defaults to <c>Ascending</c>); <see cref="TypeKey"/> format and uniqueness are validated
/// server-side.
/// </summary>
public sealed record CreateKpiTypeRequestDto(string TypeKey, string LabelAr, string LabelEn, string? ScoringDirection);

/// <summary>200 body for <c>GET /api/v1/kpi-types</c>.</summary>
public sealed record KpiTypesResponse
{
    public required IReadOnlyList<PlatformKpiTypeDto> PlatformStandardTypes { get; init; }
    public required IReadOnlyList<TenantKpiTypeDto> TenantDefinedTypes { get; init; }
}

/// <summary>A platform-standard KPI type (built-in; no id — keyed by its <c>typeKey</c>).</summary>
public sealed record PlatformKpiTypeDto
{
    public string TypeKey { get; init; } = string.Empty;
    public string LabelAr { get; init; } = string.Empty;
    public string LabelEn { get; init; } = string.Empty;
    public string ScoringDirection { get; init; } = "Ascending";
}

/// <summary>A tenant-defined custom KPI type (row in <c>kpi_type_definitions</c>).</summary>
public sealed record TenantKpiTypeDto
{
    public Guid KpiTypeDefinitionId { get; init; }
    public string TypeKey { get; init; } = string.Empty;
    public string LabelAr { get; init; } = string.Empty;
    public string LabelEn { get; init; } = string.Empty;
    public string ScoringDirection { get; init; } = "Ascending";
}

/// <summary>201 body for <c>POST /api/v1/kpi-types</c>.</summary>
public sealed record CreateKpiTypeResponse
{
    public Guid KpiTypeDefinitionId { get; init; }
    public string TypeKey { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
