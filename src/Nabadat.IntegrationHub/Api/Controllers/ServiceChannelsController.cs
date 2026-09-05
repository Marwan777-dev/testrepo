using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.IntegrationHub.Api.Contracts;
using Nabadat.IntegrationHub.Application.Channels;
using Nabadat.IntegrationHub.Application.Channels.Dtos;
using Nabadat.IntegrationHub.Application.Channels.Interfaces;
using Nabadat.UserManagement.Api.Contracts;
using Nabadat.UserManagement.Api.Interfaces;

namespace Nabadat.IntegrationHub.Api.Controllers;

/// <summary>
/// Service-channel endpoints (SCR-03/04, contracts/api-endpoints.md):
/// <list type="bullet">
///   <item><c>GET /api/v1/integration-hub/service-channels</c> — cursor-paginated list with SCR-03's
///   supported/required/integration counts (FR-S3-01).</item>
///   <item><c>GET .../{id}</c> — one channel plus its full parameter contract, for SCR-04's edit form.</item>
///   <item><c>POST .../</c> — create (FR-S4-01…04).</item>
///   <item><c>PUT .../{id}</c> — update; a post-lock channel-ID change is a 409 (BR-05).</item>
/// </list>
///
/// <para><b>There is deliberately NO <c>DELETE</c> route</b> — BR-07 / FR-S3-02: a channel is deactivated,
/// never deleted, and one that has ever received traffic can never be removed at all. The absence of the
/// route is the enforcement; adding one would be a spec violation.</para>
///
/// <para>Authentication is enforced by <c>[Authorize]</c> against the host's PortalSession scheme, and the
/// actor (user id / persona) is read from <see cref="ISessionContextAccessor"/> and passed down on the
/// command — the Application layer never touches HTTP or session state.
/// <b>TODO(US9)</b>: per-persona permission gating (<c>m13.channel.view</c> / <c>m13.channel.manage</c>,
/// P-01 manage + P-07 read-only per BR-24) lands with T146, which adds the <c>RequirePermission</c>
/// attributes across every M-13 controller at once so the Permissions Matrix is applied in one consistent
/// pass rather than guessed at per story.</para>
///
/// <para>Every non-2xx response uses the shared API-05 envelope
/// (<see cref="ApiErrorEnvelope"/>), with one <c>details</c> entry per accumulated validation failure so
/// SCR-04 can attach each message to its own field.</para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/integration-hub/service-channels")]
public sealed class ServiceChannelsController : ControllerBase
{
    private readonly IServiceChannelService _channels;
    private readonly ISessionContextAccessor _session;

    public ServiceChannelsController(IServiceChannelService channels, ISessionContextAccessor session)
    {
        _channels = channels;
        _session = session;
    }

    /// <summary>
    /// GET — one cursor page of service channels, ordered by EN name. <c>limit</c> outside 1…200 falls back to
    /// the default page size rather than erroring, matching the M-06 convention.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ServiceChannelListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListChannels(
        [FromQuery] string? cursor = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var page = await _channels.ListAsync(cursor, limit, ct);

        return Ok(new ServiceChannelListResponse
        {
            Items = page.Items.Select(ToListItem).ToList(),
            NextCursor = page.NextCursor,
        });
    }

    /// <summary>GET /{id} — one channel with its parameter contract; 404 when absent.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ServiceChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChannel([FromRoute] Guid id, CancellationToken ct = default)
    {
        var channel = await _channels.GetAsync(id, ct);

        return channel is null
            ? NotFound(Envelope(ChannelErrorCodes.ChannelNotFound, "Service channel not found"))
            : Ok(ToResponse(channel));
    }

    /// <summary>
    /// POST — creates a channel and its contract. 201 with the persisted channel; 409 on a duplicate EN name
    /// or channel ID (both case-insensitive, VR-F02/F04); 400 on any other validation failure including the
    /// VR-F13 capacity ceiling.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceChannelResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateChannel(
        [FromBody] CreateServiceChannelRequest request,
        CancellationToken ct = default)
    {
        var (actorId, persona) = Actor();

        var result = await _channels.CreateAsync(
            new ServiceChannelCreateCommand(
                request.NameEn,
                request.NameAr,
                request.ChannelId,
                request.Description,
                request.Active,
                ToContract(request.Contract),
                actorId,
                persona),
            ct);

        if (!result.Succeeded)
        {
            return Failure(result.Errors);
        }

        var body = ToResponse(result.Channel!);
        return CreatedAtAction(nameof(GetChannel), new { id = body.Id }, body);
    }

    /// <summary>
    /// PUT /{id} — updates a channel and replaces its contract wholesale. 409 <c>channel.id_locked</c> when the
    /// request changes the ID of a channel that has already served a 2xx request (BR-05); 404 when the channel
    /// does not exist.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ServiceChannelResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorEnvelope), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateChannel(
        [FromRoute] Guid id,
        [FromBody] UpdateServiceChannelRequest request,
        CancellationToken ct = default)
    {
        var (actorId, persona) = Actor();

        var result = await _channels.UpdateAsync(
            id,
            new ServiceChannelUpdateCommand(
                request.NameEn,
                request.NameAr,
                request.ChannelId,
                request.Description,
                request.Active,
                ToContract(request.Contract),
                actorId,
                persona),
            ct);

        return result.Succeeded
            ? Ok(ToResponse(result.Channel!))
            : Failure(result.Errors);
    }

    // ── mapping + envelope helpers ─────────────────────────────────────────────

    private static IReadOnlyList<ChannelParameterAssignmentInput>? ToContract(
        IReadOnlyList<ChannelParameterAssignmentPayload>? payload) =>
        payload?.Select(row => new ChannelParameterAssignmentInput(row.ParameterId, row.Supported, row.Required))
            .ToList();

    private static ServiceChannelResponse ToResponse(ServiceChannelDto dto) => new()
    {
        Id = dto.Id,
        NameEn = dto.NameEn,
        NameAr = dto.NameAr,
        ChannelId = dto.ChannelId,
        Description = dto.Description,
        Active = dto.Active,
        ChannelIdLocked = dto.ChannelIdLocked,
        SupportedCount = dto.SupportedCount,
        RequiredCount = dto.RequiredCount,
        IntegrationsCount = dto.IntegrationsCount,
        Contract = dto.Contract.Select(row => new ChannelContractRowResponse
        {
            ParameterId = row.ParameterId,
            ApiField = row.ApiField,
            NameEn = row.NameEn,
            NameAr = row.NameAr,
            Supported = row.Supported,
            Required = row.Required,
        }).ToList(),
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
    };

    private static ServiceChannelListItemResponse ToListItem(ServiceChannelDto dto) => new()
    {
        Id = dto.Id,
        NameEn = dto.NameEn,
        NameAr = dto.NameAr,
        ChannelId = dto.ChannelId,
        Description = dto.Description,
        Active = dto.Active,
        ChannelIdLocked = dto.ChannelIdLocked,
        SupportedCount = dto.SupportedCount,
        RequiredCount = dto.RequiredCount,
        IntegrationsCount = dto.IntegrationsCount,
    };

    /// <summary>
    /// Maps accumulated validation failures onto a status per contracts/api-endpoints.md: a duplicate or a
    /// locked ID is a <b>conflict with existing state</b> (409), a missing channel is a 404, and everything
    /// else is a malformed submission (400). The chosen error leads the envelope; the full set travels in
    /// <c>details</c> so SCR-04 renders every inline message in one pass.
    /// </summary>
    private IActionResult Failure(IReadOnlyList<ChannelValidationError> errors)
    {
        var lead = errors.FirstOrDefault(e => e.Code == ChannelErrorCodes.ChannelNotFound)
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

        if (lead.Code == ChannelErrorCodes.ChannelNotFound)
        {
            return NotFound(envelope);
        }

        return IsConflict(lead) ? Conflict(envelope) : BadRequest(envelope);
    }

    private static bool IsConflict(ChannelValidationError error) =>
        error.Code is ChannelErrorCodes.DuplicateName
            or ChannelErrorCodes.DuplicateChannelId
            or ChannelErrorCodes.ChannelIdLocked;

    private ApiErrorEnvelope Envelope(string code, string message) => new()
    {
        Error = new ApiErrorDetail
        {
            Code = code,
            Message = message,
            CorrelationId = Correlation().ToString(),
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
