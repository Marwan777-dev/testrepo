using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Personas;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Persona CRUD and lifecycle endpoints (T071 / US-3). Implements the six operations per
/// <c>contracts/personas-api.md</c>:
/// <list type="bullet">
///   <item><description><c>GET /api/v1/personas</c> — list personas (optional status filter) with their journey-binding counts (<c>journey.read</c>)</description></item>
///   <item><description><c>POST /api/v1/personas</c> — create a Draft persona (P-01, <c>journey.personas.write</c>)</description></item>
///   <item><description><c>GET /api/v1/personas/{personaId}</c> — full persona detail incl. journey bindings (<c>journey.read</c>)</description></item>
///   <item><description><c>PUT /api/v1/personas/{personaId}</c> — update metadata; Archived is immutable (P-01, <c>journey.personas.write</c>)</description></item>
///   <item><description><c>PATCH /api/v1/personas/{personaId}/status</c> — lifecycle transition; archive blocked while bound (P-01, <c>journey.personas.publish</c>)</description></item>
///   <item><description><c>DELETE /api/v1/personas/{personaId}</c> — unsupported; returns 405 <c>persona.use_archive_instead</c></description></item>
/// </list>
/// Reads/writes delegate to <see cref="PersonaService"/>; lifecycle transitions delegate to
/// <see cref="PersonaStatusTransitionService"/>. Every non-2xx response follows the API-05 envelope.
/// </summary>
// Authentication is enforced by [Authorize] against the host's PortalSession scheme (missing/invalid
// session → 401 + API-05 envelope). Fine-grained authorization (the journey.read /
// journey.personas.write / journey.personas.publish permissions and the P-01-only restriction on
// create / update / status per contracts/personas-api.md) is still deferred to the M-10
// authorization integration — no authorization POLICY is declared here yet.
[ApiController]
[Authorize]
[Route("api/v1/personas")]
public sealed class PersonasController : ControllerBase
{
    private readonly PersonaService _personas;
    private readonly PersonaStatusTransitionService _statusTransitions;
    private readonly ISessionContextAccessor _sessionAccessor;
    private readonly TimeProvider _time;

    public PersonasController(
        PersonaService personas,
        PersonaStatusTransitionService statusTransitions,
        ISessionContextAccessor sessionAccessor,
        TimeProvider time)
    {
        _personas = personas;
        _statusTransitions = statusTransitions;
        _sessionAccessor = sessionAccessor;
        _time = time;
    }

    /// <summary>
    /// GET /api/v1/personas — Returns the tenant's personas (optionally filtered by lifecycle
    /// status), each with its journey-binding count. Required permission: journey.read.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PersonaListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PersonaListResponse>> ListPersonas(
        [FromQuery] string? status = null,
        [FromQuery] int page_size = 50,
        [FromQuery] string? page_token = null,
        CancellationToken ct = default)
    {
        if (page_size < 1 || page_size > 200)
        {
            page_size = 50;
        }

        var result = await _personas.ListPersonasAsync(status, ct);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, Envelope(result.Error!));
        }

        // One grouped query yields every persona's binding count (no N+1); personas with no
        // bindings are absent from the map and default to 0.
        var bindingCounts = await _personas.GetBindingCountsAsync(ct);

        var items = result.Value!
            .Select(p => new PersonaListItem
            {
                PersonaId = p.PersonaId,
                NameAr = p.NameAr,
                NameEn = p.NameEn,
                Status = p.Status,
                JourneyBindingCount = bindingCounts.TryGetValue(p.PersonaId, out var count) ? count : 0,
                UpdatedAt = p.UpdatedAt.UtcDateTime
            })
            .ToList();

        // The persona store is a small, unpaginated set: every persona is returned in a single
        // page (no cursor), so nextPageToken is always null. page_size/page_token are accepted for
        // forward-compatibility with the contract surface.
        return Ok(new PersonaListResponse
        {
            Items = items,
            NextPageToken = null,
            TotalCount = items.Count
        });
    }

    /// <summary>
    /// POST /api/v1/personas — Creates a persona with status Draft and publishes persona.created.
    /// Required permission: journey.personas.write (P-01 only).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreatePersonaResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreatePersonaResponse>> CreatePersona(
        [FromBody] CreatePersonaRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new CreatePersonaRequest(
            request.NameAr,
            request.NameEn,
            request.DescriptionAr,
            request.DescriptionEn);

        var result = await _personas.CreatePersonaAsync(serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var personaId = result.Value!;
        return CreatedAtAction(
            nameof(GetPersona),
            new { personaId },
            new CreatePersonaResponse
            {
                PersonaId = personaId,
                Status = PersonaStatus.Draft.ToString(),
                CreatedAt = _time.GetUtcNow().UtcDateTime
            });
    }

    /// <summary>
    /// GET /api/v1/personas/{personaId} — Returns full persona detail including the journeys it is
    /// bound to. Required permission: journey.read.
    /// </summary>
    [HttpGet("{personaId:guid}")]
    [ProducesResponseType(typeof(PersonaDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonaDetailResponse>> GetPersona(
        [FromRoute] Guid personaId,
        CancellationToken ct = default)
    {
        var result = await _personas.GetPersonaAsync(personaId, ct);
        if (!result.IsSuccess)
        {
            return NotFound(Envelope(result.Error!));
        }

        var persona = result.Value!;
        var bindings = await _personas.ListJourneyBindingsAsync(personaId, ct);

        return Ok(new PersonaDetailResponse
        {
            PersonaId = persona.PersonaId,
            NameAr = persona.NameAr,
            NameEn = persona.NameEn,
            DescriptionAr = persona.DescriptionAr,
            DescriptionEn = persona.DescriptionEn,
            Status = persona.Status,
            JourneyBindings = bindings
                .Select(b => new PersonaJourneyBindingDto { JourneyId = b.JourneyId, JourneyName = b.JourneyName })
                .ToList(),
            CreatedAt = persona.CreatedAt.UtcDateTime,
            UpdatedAt = persona.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// PUT /api/v1/personas/{personaId} — Updates persona metadata. Archived personas are immutable
    /// (403 persona.archived_immutable). Required permission: journey.personas.write (P-01 only).
    /// </summary>
    [HttpPut("{personaId:guid}")]
    [ProducesResponseType(typeof(UpdatePersonaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UpdatePersonaResponse>> UpdatePersona(
        [FromRoute] Guid personaId,
        [FromBody] UpdatePersonaRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new UpdatePersonaRequest(
            request.NameAr,
            request.NameEn,
            request.DescriptionAr,
            request.DescriptionEn);

        var result = await _personas.UpdatePersonaAsync(personaId, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var persona = result.Value!;
        return Ok(new UpdatePersonaResponse
        {
            PersonaId = persona.PersonaId,
            UpdatedAt = persona.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// PATCH /api/v1/personas/{personaId}/status — Transitions the persona lifecycle status,
    /// publishing persona.status.changed. Archiving is blocked while the persona is bound to
    /// journeys (409 persona.archive_blocked_active_bindings). Required permission:
    /// journey.personas.publish (P-01 only).
    /// </summary>
    [HttpPatch("{personaId:guid}/status")]
    [ProducesResponseType(typeof(PersonaStatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonaStatusChangeResponse>> ChangeStatus(
        [FromRoute] Guid personaId,
        [FromBody] ChangePersonaStatusRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        if (!Enum.TryParse<PersonaStatus>(request.Status, ignoreCase: false, out var targetStatus))
        {
            return UnprocessableEntity(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = "persona.validation_error",
                    Message = $"Invalid persona status '{request.Status}'."
                }
            });
        }

        var result = await _statusTransitions.ChangeStatusAsync(personaId, targetStatus, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        // Re-fetch so the response carries the persisted status and updated-at stamped in the
        // transition's transaction.
        var getResult = await _personas.GetPersonaAsync(personaId, ct);
        if (!getResult.IsSuccess)
        {
            return NotFound(Envelope(getResult.Error!));
        }

        var persona = getResult.Value!;
        return Ok(new PersonaStatusChangeResponse
        {
            PersonaId = persona.PersonaId,
            Status = persona.Status,
            UpdatedAt = persona.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// DELETE /api/v1/personas/{personaId} — Hard deletion is unsupported; archiving (PATCH
    /// .../status → Archived) is the terminal action. Always returns 405 with
    /// persona.use_archive_instead.
    /// </summary>
    [HttpDelete("{personaId:guid}")]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status405MethodNotAllowed)]
    public IActionResult DeletePersona([FromRoute] Guid personaId)
    {
        return StatusCode(StatusCodes.Status405MethodNotAllowed, new ApiErrorResponse
        {
            Error = new ApiErrorDetail
            {
                Code = "persona.use_archive_instead",
                Message = "Personas cannot be deleted; archive the persona via PATCH /status instead."
            }
        });
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
    /// Maps a persona-service failure <see cref="Error"/> onto the HTTP status defined for it in
    /// <c>contracts/personas-api.md</c>, wrapped in the API-05 envelope. Unknown codes default to
    /// 422 (validation), matching the module's controller convention.
    /// </summary>
    private ObjectResult MapError(Error error) => error.Code switch
    {
        "persona.not_found" => NotFound(Envelope(error)),
        "persona.archived_immutable" => StatusCode(StatusCodes.Status403Forbidden, Envelope(error)),
        "persona.archive_blocked_active_bindings" => Conflict(Envelope(error)),
        _ => UnprocessableEntity(Envelope(error))
    };

    /// <summary>Wraps an <see cref="Error"/> in the API-05 response envelope.</summary>
    private static ApiErrorResponse Envelope(Error error) => new()
    {
        Error = new ApiErrorDetail { Code = error.Code, Message = error.Message }
    };
}

/// <summary>API request/response DTOs for persona endpoints.</summary>

public sealed record CreatePersonaRequestDto(string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn);

public sealed record UpdatePersonaRequestDto(string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn);

public sealed record ChangePersonaStatusRequestDto(string Status);

public sealed record PersonaListItem
{
    public Guid PersonaId { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int JourneyBindingCount { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record PersonaListResponse
{
    public required IReadOnlyList<PersonaListItem> Items { get; init; }
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}

public sealed record CreatePersonaResponse
{
    public Guid PersonaId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed record PersonaJourneyBindingDto
{
    public Guid JourneyId { get; init; }
    public string JourneyName { get; init; } = string.Empty;
}

public sealed record PersonaDetailResponse
{
    public Guid PersonaId { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string? DescriptionAr { get; init; }
    public string? DescriptionEn { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<PersonaJourneyBindingDto> JourneyBindings { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record UpdatePersonaResponse
{
    public Guid PersonaId { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record PersonaStatusChangeResponse
{
    public Guid PersonaId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}
