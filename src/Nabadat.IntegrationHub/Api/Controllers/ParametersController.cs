using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.IntegrationHub.Api.Contracts;
using Nabadat.IntegrationHub.Application.Parameters;
using Nabadat.IntegrationHub.Application.Parameters.Dtos;
using Nabadat.IntegrationHub.Application.Parameters.Exceptions;
using Nabadat.IntegrationHub.Application.Parameters.Interfaces;
using Nabadat.IntegrationHub.Domain.ValueObjects;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.IntegrationHub.Api.Controllers;

/// <summary>
/// Parameter-catalogue endpoints (SCR-05/06, contracts/api-endpoints.md):
/// <list type="bullet">
///   <item><c>GET /api/v1/integration-hub/parameters</c> — cursor-paginated list with the AND-combined
///   origin/type/search filters and SCR-05's global origin-tab counts (FR-S5-01).</item>
///   <item><c>GET .../{id}</c> — one parameter for SCR-06's drawer.</item>
///   <item><c>POST .../</c> — create a custom parameter (FR-S6-02…05).</item>
///   <item><c>PATCH .../{id}</c> — partial update; also SCR-05's inline enable/disable toggle, carrying BR-10's
///   impact-warning flow.</item>
/// </list>
///
/// <para><b>There is deliberately NO <c>DELETE</c> route</b> — BR-09: parameters of either origin are disabled,
/// never deleted, and a disabled one keeps its API field name reserved forever (VR-F06). The absence of the route
/// is the enforcement; adding one would be a spec violation. <c>BuiltInParameterGuard</c> is the second line of
/// defence behind it.</para>
///
/// <para>Authentication is enforced by <c>[Authorize]</c>; the actor is read from
/// <see cref="ISessionContextAccessor"/> and passed down on the command — the Application layer never touches HTTP
/// or session state. <b>TODO(US9)</b>: per-persona permission gating (<c>m13.parameter.view</c> /
/// <c>m13.parameter.manage</c>, P-01 manage + P-07 read-only) lands with T146, which applies the Permissions
/// Matrix across every M-13 controller in one pass.</para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/integration-hub/parameters")]
public sealed class ParametersController : ControllerBase
{
    private readonly IParameterService _parameters;
    private readonly ISessionContextAccessor _session;

    public ParametersController(IParameterService parameters, ISessionContextAccessor session)
    {
        _parameters = parameters;
        _session = session;
    }

    /// <summary>
    /// GET — one cursor page. <c>origin</c>, <c>type</c> and <c>q</c> AND-combine (FR-S5-01). An unrecognised
    /// <c>origin</c>/<c>type</c> literal is a 400 rather than a silently-ignored filter, which would otherwise
    /// return the unfiltered list and read as a data bug.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ParameterListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListParameters(
        [FromQuery] string? origin = null,
        [FromQuery] string? type = null,
        [FromQuery] string? q = null,
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        ParameterOrigin? parsedOrigin = null;
        if (!string.IsNullOrWhiteSpace(origin))
        {
            if (!ParameterWireValues.TryParseOrigin(origin, out var value))
            {
                return BadRequest(Envelope(
                    "validation.invalid_origin", "Origin must be 'built_in' or 'custom'"));
            }

            parsedOrigin = value;
        }

        DataType? parsedType = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!ParameterWireValues.TryParseDataType(type, out var value))
            {
                return BadRequest(Envelope(
                    ParameterErrorCodes.InvalidDataType, "Type must be one of the supported parameter types"));
            }

            parsedType = value;
        }

        var page = await _parameters.ListAsync(
            new ParameterListFilter(parsedOrigin, parsedType, q), cursor, limit, ct);

        return Ok(new ParameterListResponse
        {
            Items = page.Items.Select(ToResponse).ToList(),
            NextCursor = page.NextCursor,
            Counts = new ParameterCountsResponse
            {
                All = page.Counts.All,
                BuiltIn = page.Counts.BuiltIn,
                Custom = page.Counts.Custom,
            },
        });
    }

    /// <summary>GET /{id} — one parameter; 404 when absent.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ParameterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetParameter([FromRoute] Guid id, CancellationToken ct = default)
    {
        var parameter = await _parameters.GetAsync(id, ct);

        return parameter is null
            ? NotFound(Envelope(ParameterErrorCodes.ParameterNotFound, "Parameter not found"))
            : Ok(ToResponse(parameter));
    }

    /// <summary>
    /// POST — creates a custom parameter. 201 with the persisted row; 409 on a duplicate API field (VR-F06,
    /// including against a disabled or built-in parameter); 400 on any other validation failure including
    /// VR-F07's range rule and VR-F13's capacity ceiling.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ParameterResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateParameter(
        [FromBody] CreateParameterRequest request,
        CancellationToken ct = default)
    {
        if (!ParameterWireValues.TryParseDataType(request.DataType, out var dataType))
        {
            // Caught here rather than in the service because an unparseable literal has no enum value to hand down
            // — [PO-G17]'s rejected "duration"/"identifier" land in exactly this branch.
            return BadRequest(Envelope(
                ParameterErrorCodes.InvalidDataType,
                "Select one of the supported parameter types",
                ParameterFields.DataType));
        }

        var (actorId, persona) = Actor();

        var result = await _parameters.CreateAsync(
            new ParameterCreateCommand(
                request.NameEn,
                request.NameAr,
                request.ApiField,
                dataType,
                request.RangeMin,
                request.RangeMax,
                request.RangeUnit,
                request.ValidationRule,
                request.Enabled,
                request.RequiredByDefault,
                request.Filterable,
                request.ReportingVisibility,
                request.DashboardVisibility,
                request.MappingSupport,
                request.ChannelIds,
                actorId,
                persona),
            ct);

        if (!result.Succeeded)
        {
            return Failure(result.Errors);
        }

        var body = ToResponse(result.Parameter!);
        return CreatedAtAction(nameof(GetParameter), new { id = body.Id }, body);
    }

    /// <summary>
    /// PATCH /{id} — applies a partial update. 409 on a locked API field (BR-11) or a built-in type change
    /// (<c>[PO-G27]</c>); 404 when the parameter does not exist.
    ///
    /// <para>A disable on a referenced parameter returns <b>200</b> with <c>requires_confirmation</c> and BR-10's
    /// reference list, leaving the parameter unchanged — see <see cref="PatchParameterResponse"/> for why that is a
    /// 200 and not a 4xx.</para>
    /// </summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(PatchParameterResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PatchParameter(
        [FromRoute] Guid id,
        [FromBody] PatchParameterRequest request,
        CancellationToken ct = default)
    {
        DataType? dataType = null;
        if (!string.IsNullOrWhiteSpace(request.DataType))
        {
            if (!ParameterWireValues.TryParseDataType(request.DataType, out var value))
            {
                return BadRequest(Envelope(
                    ParameterErrorCodes.InvalidDataType,
                    "Select one of the supported parameter types",
                    ParameterFields.DataType));
            }

            dataType = value;
        }

        var (actorId, persona) = Actor();

        ParameterSaveResult result;
        try
        {
            result = await _parameters.PatchAsync(
                id,
                new ParameterPatchCommand(
                    request.NameEn,
                    request.NameAr,
                    request.ApiField,
                    dataType,
                    request.RangeMin,
                    request.RangeMax,
                    request.RangeUnit,
                    request.ValidationRule,
                    request.Enabled,
                    request.RequiredByDefault,
                    request.Filterable,
                    request.ReportingVisibility,
                    request.DashboardVisibility,
                    request.MappingSupport,
                    request.ChannelIds,
                    request.ConfirmDisable,
                    actorId,
                    persona),
                ct);
        }
        catch (BuiltInParameterViolationException ex)
        {
            // BuiltInParameterGuard throws rather than accumulating — these are operations the API does not offer,
            // not correctable field errors, so they arrive here instead of in result.Errors.
            return Conflict(Envelope(ex.Code, ex.Message));
        }

        if (!result.Succeeded)
        {
            return Failure(result.Errors);
        }

        return Ok(new PatchParameterResponse
        {
            Parameter = ToResponse(result.Parameter!),
            RequiresConfirmation = result.RequiresDisableConfirmation,
            References = result.References.Select(ToResponse).ToList(),
        });
    }

    // ── mapping + envelope helpers ─────────────────────────────────────────────

    private static ParameterResponse ToResponse(ParameterDto dto) => new()
    {
        Id = dto.Id,
        NameEn = dto.NameEn,
        NameAr = dto.NameAr,
        ApiField = dto.ApiField,
        ApiFieldLocked = dto.ApiFieldLocked,
        DataType = ParameterWireValues.ToWire(dto.DataType),
        DataTypeLocked = dto.DataTypeLocked,
        RangeMin = dto.RangeMin,
        RangeMax = dto.RangeMax,
        RangeUnit = dto.RangeUnit,
        ValidationRule = dto.ValidationRule,
        Origin = ParameterWireValues.ToWire(dto.Origin),
        Enabled = dto.Enabled,
        RequiredByDefault = dto.RequiredByDefault,
        Filterable = dto.Filterable,
        ReportingVisibility = dto.ReportingVisibility,
        DashboardVisibility = dto.DashboardVisibility,
        MappingSupport = dto.MappingSupport,
        MappingSupportChangeable = dto.MappingSupportChangeable,
        MappingsCount = dto.MappingsCount,
        ChannelIds = dto.ChannelIds,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
    };

    private static ParameterReferenceResponse ToResponse(ParameterReference reference) => new()
    {
        Kind = reference.Kind switch
        {
            ParameterReferenceKind.ChannelContract => "channel_contract",
            ParameterReferenceKind.DataScopeFilter => "data_scope_filter",
            ParameterReferenceKind.RuleBuilder => "rule_builder",
            _ => "unknown",
        },
        Name = reference.Name,
    };

    /// <summary>
    /// Maps accumulated validation failures onto a status per contracts/api-endpoints.md: a duplicate API field or
    /// a locked field/type is a <b>conflict with existing state</b> (409), a missing parameter is a 404, and
    /// everything else is a malformed submission (400). The chosen error leads the envelope; the full set travels
    /// in <c>details</c> so SCR-06 renders every inline message in one pass.
    /// </summary>
    private IActionResult Failure(IReadOnlyList<ParameterValidationError> errors)
    {
        var lead = errors.FirstOrDefault(e => e.Code == ParameterErrorCodes.ParameterNotFound)
            ?? errors.FirstOrDefault(IsConflict)
            ?? errors[0];

        var envelope = new ApiErrorEnvelope
        {
            Error = new ApiErrorDetail
            {
                Code = lead.Code,
                Message = lead.Message,
                CorrelationId = Correlation().ToString(),
                Details = errors
                    .Select(e => new ApiErrorFieldDetail { Field = e.Field ?? string.Empty, Code = e.Code })
                    .ToList(),
            },
        };

        if (lead.Code == ParameterErrorCodes.ParameterNotFound)
        {
            return NotFound(envelope);
        }

        return IsConflict(lead) ? Conflict(envelope) : BadRequest(envelope);
    }

    private static bool IsConflict(ParameterValidationError error) =>
        error.Code is ParameterErrorCodes.DuplicateApiField
            or ParameterErrorCodes.ApiFieldLocked
            or ParameterErrorCodes.ParameterTypeLocked;

    private ApiErrorEnvelope Envelope(string code, string message, string? field = null) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = Correlation().ToString(),
            Details = field is null
                ? null
                : new List<ApiErrorFieldDetail> { new() { Field = field, Code = code } },
        },
    };

    /// <summary>
    /// The authenticated actor for the audit row. <c>[Authorize]</c> guarantees a session, so the fallback is
    /// unreachable in practice; it keeps the audit write attributable rather than throwing.
    /// </summary>
    private (Guid ActorId, string? Persona) Actor()
    {
        var session = _session.Current;
        return session is null ? (Guid.Empty, null) : (session.UserId, session.Persona);
    }

    private Guid Correlation() =>
        Guid.TryParse(HttpContext.TraceIdentifier, out var correlationId) ? correlationId : Guid.NewGuid();
}
