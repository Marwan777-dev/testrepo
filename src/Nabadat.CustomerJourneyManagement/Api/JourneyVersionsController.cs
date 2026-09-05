using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Versioning;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Journey version (immutable snapshot) endpoints (T072 / US-3). Implements the three version
/// operations per <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description><c>POST /api/v1/journeys/{id}/publish</c> — freeze the current journey tree as the next immutable version (P-01, <c>journey.publish</c>)</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/versions</c> — cursor-paginated version list, newest first (<c>journey.read</c>)</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/versions/{versionNumber}</c> — the stored snapshot verbatim, marked <c>isSnapshot: true</c> + <c>snapshotVersion</c> (<c>journey.read</c>)</description></item>
/// </list>
/// All work delegates to <see cref="JourneyVersionService"/>; the snapshot read returns the frozen
/// <c>snapshot_payload</c> JSON exactly as captured at publish time (never a recomputed tree). Every
/// non-2xx response follows the API-05 envelope.
/// </summary>
// Authentication is enforced by [Authorize] against the host's PortalSession scheme (missing/invalid
// session → 401 + API-05 envelope). Fine-grained authorization (the journey.read / journey.publish
// permissions and the P-01-only restriction on publish per contracts/journeys-api.md) is still
// deferred to the M-10 authorization integration — no authorization POLICY is declared here yet. The
// live P-01/P-02 split is exercised by the Docker-gated T074 (JourneyVersionsEndpointTests).
[ApiController]
[Authorize]
[Route("api/v1/journeys")]
public sealed class JourneyVersionsController : ControllerBase
{
    private readonly JourneyVersionService _versions;
    private readonly ISessionContextAccessor _sessionAccessor;

    public JourneyVersionsController(
        JourneyVersionService versions,
        ISessionContextAccessor sessionAccessor)
    {
        _versions = versions;
        _sessionAccessor = sessionAccessor;
    }

    /// <summary>
    /// POST /api/v1/journeys/{id}/publish — Publishes the current journey configuration as the next
    /// immutable version (snapshot row + journey.version.published in one tx) and returns the new
    /// version's id, number, and publish timestamp. Required permission: journey.publish (P-01 only).
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(typeof(PublishVersionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PublishVersionResponse>> PublishVersion(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var result = await _versions.PublishJourneyVersionAsync(id, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var versionNumber = result.Value;

        // Re-read the just-written row so the 201 body carries the persisted versionId and
        // publishedAt stamped inside the publish transaction (the publish call returns only the
        // version number). Mirrors the re-fetch pattern used by the status-change endpoints.
        var read = await _versions.GetJourneyVersionAsync(id, versionNumber, ct);
        if (!read.IsSuccess)
        {
            // The version was written but cannot be read back — an internal inconsistency.
            return StatusCode(StatusCodes.Status500InternalServerError, Envelope(read.Error!));
        }

        var version = read.Value!;
        return CreatedAtAction(
            nameof(GetVersion),
            new { id, versionNumber },
            new PublishVersionResponse
            {
                VersionId = version.VersionId,
                VersionNumber = version.VersionNumber,
                PublishedAt = version.PublishedAt.UtcDateTime
            });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id}/versions — Returns the journey's published versions newest first,
    /// cursor-paginated (API-04). Required permission: journey.read.
    /// </summary>
    [HttpGet("{id:guid}/versions")]
    [ProducesResponseType(typeof(VersionListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VersionListResponse>> ListVersions(
        [FromRoute] Guid id,
        [FromQuery] int page_size = 20,
        [FromQuery] string? page_token = null,
        CancellationToken ct = default)
    {
        if (page_size < 1 || page_size > 200)
        {
            page_size = 20;
        }

        var page = await _versions.ListJourneyVersionsAsync(id, page_size, page_token, ct);

        var items = page.Items
            .Select(v => new VersionListItem
            {
                VersionId = v.VersionId,
                VersionNumber = v.VersionNumber,
                PublishedAt = v.PublishedAt.UtcDateTime,
                // publishedBy is an M-10 user_id (no cross-module FK); resolving it to a display name
                // is a Phase 3+ M-10 lookup, left empty for now (same deferral as the journey
                // updated-at endpoint's updatedByName).
                PublishedByName = string.Empty
            })
            .ToList();

        return Ok(new VersionListResponse
        {
            Items = items,
            NextPageToken = page.NextCursor,
            TotalCount = (int)page.TotalCount
        });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id}/versions/{versionNumber} — Returns the full journey snapshot captured
    /// at publish time, exactly as stored (never recomputed). The payload shape matches
    /// GET /api/v1/journeys/{id} but is marked <c>isSnapshot: true</c> and carries
    /// <c>snapshotVersion</c>. Returns 404 journey.version_not_found when the version does not exist.
    /// Required permission: journey.read.
    /// </summary>
    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersion(
        [FromRoute] Guid id,
        [FromRoute] int versionNumber,
        CancellationToken ct = default)
    {
        var result = await _versions.GetJourneyVersionAsync(id, versionNumber, ct);
        if (!result.IsSuccess)
        {
            return NotFound(Envelope(result.Error!));
        }

        var version = result.Value!;

        // The stored snapshot is opaque JSON frozen at publish time. Re-hydrate it to an object and
        // graft the two read-only markers the contract requires (isSnapshot / snapshotVersion) so the
        // response is self-describing as a historical snapshot, not a live tree.
        if (JsonNode.Parse(version.SnapshotPayload) is not JsonObject snapshot)
        {
            // Defensive: a published payload is always a JSON object; anything else is corrupt data.
            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = "journey.snapshot_corrupt",
                    Message = $"Snapshot for version {versionNumber} of journey {id} is not a valid object."
                }
            });
        }

        snapshot["isSnapshot"] = true;
        snapshot["snapshotVersion"] = versionNumber;
        return Ok(snapshot);
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
    /// Maps a version-service failure <see cref="Error"/> onto the HTTP status defined for it in
    /// <c>contracts/journeys-api.md</c>, wrapped in the API-05 envelope. Unknown codes default to 422
    /// (validation), matching the module's controller convention.
    /// </summary>
    private ObjectResult MapError(Error error) => error.Code switch
    {
        "journey.not_found" => NotFound(Envelope(error)),
        "journey.version_not_found" => NotFound(Envelope(error)),
        "journey.archived_immutable" => StatusCode(StatusCodes.Status403Forbidden, Envelope(error)),
        _ => UnprocessableEntity(Envelope(error))
    };

    /// <summary>Wraps an <see cref="Error"/> in the API-05 response envelope.</summary>
    private static ApiErrorResponse Envelope(Error error) => new()
    {
        Error = new ApiErrorDetail { Code = error.Code, Message = error.Message }
    };
}

/// <summary>API response DTOs for journey version endpoints.</summary>

/// <summary>201 body for <c>POST /api/v1/journeys/{id}/publish</c>.</summary>
public sealed record PublishVersionResponse
{
    public Guid VersionId { get; init; }
    public int VersionNumber { get; init; }
    public DateTime PublishedAt { get; init; }
}

public sealed record VersionListItem
{
    public Guid VersionId { get; init; }
    public int VersionNumber { get; init; }
    public DateTime PublishedAt { get; init; }
    public string PublishedByName { get; init; } = string.Empty;
}

public sealed record VersionListResponse
{
    public required IReadOnlyList<VersionListItem> Items { get; init; }
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}
