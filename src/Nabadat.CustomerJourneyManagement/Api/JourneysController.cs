using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Journey CRUD and lifecycle endpoints. Implements six operations (<c>contracts/journeys-api.md</c>):
/// <list type="bullet">
///   <item><description><c>GET /api/v1/journeys</c> — cursor-paginated list (optional status filter) (T028/US-1)</description></item>
///   <item><description><c>POST /api/v1/journeys</c> — create journey with Draft status (T028/US-1)</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}</c> — full journey tree (journey → stages → touchpoints) (T028/US-1)</description></item>
///   <item><description><c>PUT /api/v1/journeys/{id}</c> — update journey metadata (T028/US-1)</description></item>
///   <item><description><c>PATCH /api/v1/journeys/{id}/status</c> — lifecycle status transition (P-01 only) (T028/US-1)</description></item>
///   <item><description><c>GET /api/v1/journeys/{id}/updated-at</c> — concurrent-edit polling endpoint (T028/US-1)</description></item>
/// </list>
/// Strategic scoring is <b>tenant-level</b> (SRS §4.2.9 / §11.7, Q11 RESOLVED — per-tenant, not
/// per-journey): the former <c>PUT|GET /api/v1/journeys/{id}/scoring</c> endpoints are removed (US-2
/// Amendment). Scoring is read/written via the published <c>IScoringConfigStore</c>; the editing UI is
/// the Platform Settings → Customer Journey page (feature 003). Every endpoint resolves tenant from JWT
/// (API-02) and enforces authorization (API-03). All responses follow the API-05 error envelope.
/// </summary>
// Authentication is enforced by [Authorize] against the host's PortalSession scheme (missing/invalid
// session → 401 + API-05 envelope). Fine-grained authorization (the journey.read/write/publish
// permissions documented in contracts/journeys-api.md) is still deferred to the M-10 authorization
// integration — no authorization POLICY is declared here yet.
[ApiController]
[Authorize]
[Route("api/v1/journeys")]
public sealed class JourneysController : ControllerBase
{
    private readonly JourneyService _journeys;
    private readonly JourneyStatusTransitionService _statusTransitions;
    private readonly ISessionContextAccessor _sessionAccessor;
    private readonly TimeProvider _time;

    public JourneysController(
        JourneyService journeys,
        JourneyStatusTransitionService statusTransitions,
        ISessionContextAccessor sessionAccessor,
        TimeProvider time)
    {
        _journeys = journeys;
        _statusTransitions = statusTransitions;
        _sessionAccessor = sessionAccessor;
        _time = time;
    }

    /// <summary>
    /// GET /api/v1/journeys — Returns a paginated list of journeys for the authenticated tenant.
    /// Required permission: journey.read
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(JourneyListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<JourneyListResponse>> ListJourneys(
        [FromQuery] string? status = null,
        [FromQuery] int page_size = 50,
        [FromQuery] string? page_token = null,
        CancellationToken ct = default)
    {
        if (page_size < 1 || page_size > 200)
        {
            page_size = 50;
        }

        var result = await _journeys.ListJourneysAsync(status, page_size, page_token, ct);
        if (!result.IsSuccess)
        {
            return StatusCode(500, new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = result.Error!.Code,
                    Message = result.Error.Message
                }
            });
        }

        var page = result.Value!;
        var items = page.Items.Select(j => new JourneyListItem
        {
            JourneyId = j.JourneyId,
            Name = j.Name,
            Description = j.Description,
            JourneyType = j.JourneyType,
            Status = j.Status,
            StageCount = 0,  // Will be computed in US-2
            TouchpointCount = 0,  // Will be computed in US-2
            UpdatedAt = j.UpdatedAt.UtcDateTime,
            UpdatedBy = j.UpdatedBy ?? j.CreatedBy
        }).ToList();

        return Ok(new JourneyListResponse
        {
            Items = items,
            NextPageToken = page.NextCursor,
            TotalCount = (int)page.TotalCount
        });
    }

    /// <summary>
    /// POST /api/v1/journeys — Creates a new journey with status Draft.
    /// Required permission: journey.write
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateJourneyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateJourneyResponse>> CreateJourney(
        [FromBody] CreateJourneyRequestDto request,
        CancellationToken ct = default)
    {
        var session = _sessionAccessor.Current!;

        var actor = new ActorContext(
            session.UserId,
            session.Persona,
            HttpContext.CorrelationId());

        var serviceRequest = new CreateJourneyRequest(
            request.Name,
            request.Description,
            request.JourneyType);

        var result = await _journeys.CreateJourneyAsync(serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.Code switch
            {
                "journey.name_conflict" => Conflict(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                _ => UnprocessableEntity(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                })
            };
        }

        var journeyId = result.Value!;
        var now = _time.GetUtcNow();
        return CreatedAtAction(
            nameof(GetJourney),
            new { id = journeyId },
            new CreateJourneyResponse
            {
                JourneyId = journeyId,
                Name = request.Name,
                Status = "Draft",
                CreatedAt = now.UtcDateTime
            });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id} — Returns the full journey tree (journey → stages → touchpoints).
    /// Required permission: journey.read
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(JourneyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JourneyDetailResponse>> GetJourney(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var result = await _journeys.GetJourneyAsync(id, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = result.Error!.Code,
                    Message = result.Error.Message
                }
            });
        }

        var tree = result.Value!;
        var stages = tree.Stages.Select((st, idx) => new StageDetailDto
        {
            StageId = st.Stage.StageId,
            SequenceNumber = st.Stage.SequenceNumber,
            Name = st.Stage.Name,
            Description = st.Stage.Description,
            CustomerGoal = st.Stage.CustomerGoal,
            ExpectedEmotion = st.Stage.ExpectedEmotion,
            DurationHint = st.Stage.DurationHint,
            Touchpoints = st.Touchpoints.Select(tp => new TouchpointDetailDto
            {
                TouchpointId = tp.Touchpoint.TouchpointId,
                Name = tp.Touchpoint.Name,
                Channels = tp.Touchpoint.Channels,
                Importance = tp.Touchpoint.Importance,
                IsMoT = tp.Touchpoint.IsMot,
                IsMandatory = tp.Touchpoint.IsMandatory,
                // A touchpoint with at least one binding is measured (FR-008).
                IsMeasured = tp.KpiBindings.Count > 0,
                KpiBindings = tp.KpiBindings
                    .Select(b => new TouchpointKpiBindingDto
                    {
                        KpiType = b.KpiType,
                        Weight = b.Weight,
                        IsPlatformStandard = b.IsPlatformStandard
                    })
                    .ToList()
            }).ToList()
        }).ToList();

        return Ok(new JourneyDetailResponse
        {
            JourneyId = tree.Journey.JourneyId,
            Name = tree.Journey.Name,
            Description = tree.Journey.Description,
            JourneyType = tree.Journey.JourneyType,
            Status = tree.Journey.Status,
            PersonaBindings = tree.PersonaBindings
                .Select(p => new PersonaBindingDto
                {
                    PersonaId = p.PersonaId,
                    NameAr = p.NameAr,
                    NameEn = p.NameEn
                })
                .ToList(),
            Stages = stages,
            UpdatedAt = tree.Journey.UpdatedAt.UtcDateTime,
            UpdatedBy = tree.Journey.UpdatedBy ?? tree.Journey.CreatedBy
        });
    }

    /// <summary>
    /// PUT /api/v1/journeys/{id} — Updates journey metadata (name/description/type).
    /// Required permission: journey.write
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UpdateJourneyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UpdateJourneyResponse>> UpdateJourney(
        [FromRoute] Guid id,
        [FromBody] UpdateJourneyRequestDto request,
        CancellationToken ct = default)
    {
        var session = _sessionAccessor.Current!;

        var actor = new ActorContext(
            session.UserId,
            session.Persona,
            HttpContext.CorrelationId());

        // personaIds is the full replacement set of bound personas (US-3); null/absent leaves
        // bindings unchanged. Parse each id up front — a malformed id is a 422 validation error,
        // never a binding attempt.
        IReadOnlyList<Guid>? personaIds = null;
        if (request.PersonaIds is not null)
        {
            var parsed = new List<Guid>(request.PersonaIds.Length);
            foreach (var raw in request.PersonaIds)
            {
                if (!Guid.TryParse(raw, out var personaId))
                {
                    return UnprocessableEntity(new ApiErrorResponse
                    {
                        Error = new ApiErrorDetail
                        {
                            Code = "journey.validation_error",
                            Message = $"Invalid persona id '{raw}'."
                        }
                    });
                }

                parsed.Add(personaId);
            }

            personaIds = parsed;
        }

        var serviceRequest = new UpdateJourneyRequest(
            request.Name,
            request.Description,
            request.JourneyType,
            personaIds);

        var result = await _journeys.UpdateJourneyAsync(id, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.Code switch
            {
                "journey.archived_immutable" => StatusCode(403, new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                "journey.not_found" => NotFound(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                "journey.name_conflict" => Conflict(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                _ => UnprocessableEntity(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                })
            };
        }

        var journey = result.Value!;
        return Ok(new UpdateJourneyResponse
        {
            JourneyId = journey.JourneyId,
            Name = journey.Name,
            UpdatedAt = journey.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// PATCH /api/v1/journeys/{id}/status — Transitions the journey lifecycle status (P-01 only).
    /// Required permission: journey.publish
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(StatusChangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<StatusChangeResponse>> ChangeStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeStatusRequestDto request,
        CancellationToken ct = default)
    {
        var session = _sessionAccessor.Current!;

        if (!Enum.TryParse<JourneyStatus>(request.Status, out var targetStatus))
        {
            return UnprocessableEntity(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = "journey.validation_error",
                    Message = $"Invalid status '{request.Status}'."
                }
            });
        }

        var actor = new ActorContext(
            session.UserId,
            session.Persona,
            HttpContext.CorrelationId());

        var result = await _statusTransitions.ChangeStatusAsync(id, targetStatus, actor, ct);
        if (!result.IsSuccess)
        {
            return result.Error!.Code switch
            {
                "journey.not_found" => NotFound(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                "journey.archive_blocked_active_surveys" => Conflict(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                }),
                _ => UnprocessableEntity(new ApiErrorResponse
                {
                    Error = new ApiErrorDetail
                    {
                        Code = result.Error.Code,
                        Message = result.Error.Message
                    }
                })
            };
        }

        // Re-fetch the updated journey to return the new status
        var getResult = await _journeys.GetJourneyAsync(id, ct);
        if (!getResult.IsSuccess)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiErrorDetail { Code = "journey.not_found", Message = "Journey not found." }
            });
        }

        var journey = getResult.Value!.Journey;
        var now = _time.GetUtcNow();
        return Ok(new StatusChangeResponse
        {
            JourneyId = id,
            Status = journey.Status,
            UpdatedAt = now.UtcDateTime
        });
    }

    /// <summary>
    /// GET /api/v1/journeys/{id}/updated-at — Returns the journey's last update timestamp
    /// for concurrent-edit polling.
    /// Required permission: journey.read
    /// </summary>
    [HttpGet("{id}/updated-at")]
    [ProducesResponseType(typeof(UpdatedAtResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdatedAtResponse>> GetUpdatedAt(
        [FromRoute] Guid id,
        CancellationToken ct = default)
    {
        var result = await _journeys.GetJourneyAsync(id, ct);
        if (!result.IsSuccess)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiErrorDetail
                {
                    Code = result.Error!.Code,
                    Message = result.Error.Message
                }
            });
        }

        var journey = result.Value!.Journey;
        return Ok(new UpdatedAtResponse
        {
            UpdatedAt = journey.UpdatedAt.UtcDateTime,
            UpdatedByUserId = journey.UpdatedBy ?? journey.CreatedBy,
            UpdatedByName = string.Empty  // Will be populated via M-10 lookup in Phase 3+
        });
    }

}

/// <summary>API request/response DTOs for journey endpoints.</summary>

public sealed record CreateJourneyRequestDto(string Name, string? Description, string JourneyType);

/// <summary>
/// Update-journey request body (<c>PUT /api/v1/journeys/{id}</c>). <see cref="PersonaIds"/> is the
/// full replacement set of bound persona ids (US-3, FR-005): <see langword="null"/>/absent leaves
/// bindings unchanged, a (possibly empty) array reconciles them to exactly that set. Each newly-bound
/// persona must be <c>Active</c> (else 422 <c>journey.invalid_persona</c>).
/// </summary>
public sealed record UpdateJourneyRequestDto(string Name, string? Description, string JourneyType, string[]? PersonaIds = null);

public sealed record ChangeStatusRequestDto(string Status);

public sealed record JourneyListItem
{
    public Guid JourneyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string JourneyType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int StageCount { get; init; }
    public int TouchpointCount { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Guid UpdatedBy { get; init; }
}

public sealed record JourneyListResponse
{
    public required IReadOnlyList<JourneyListItem> Items { get; init; }
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}

public sealed record CreateJourneyResponse
{
    public Guid JourneyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed record PersonaBindingDto
{
    public Guid PersonaId { get; init; }
    public string NameAr { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
}

public sealed record TouchpointDetailDto
{
    public Guid TouchpointId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string[] Channels { get; init; } = [];
    public string Importance { get; init; } = "Medium";
    public bool IsMoT { get; init; }
    public bool IsMandatory { get; init; }
    public bool IsMeasured { get; init; }
    public IReadOnlyList<TouchpointKpiBindingDto> KpiBindings { get; init; } = [];
}

/// <summary>
/// One KPI binding as embedded in the journey-tree response (<c>GET /api/v1/journeys/{id}</c>).
/// Carries only what the builder renders — <c>kpiType</c>, <c>weight</c>, <c>isPlatformStandard</c>
/// (the per-binding <c>scoringDirection</c> is resolved only on the save response, not the tree).
/// </summary>
public sealed record TouchpointKpiBindingDto
{
    public string KpiType { get; init; } = string.Empty;
    public decimal Weight { get; init; }
    public bool IsPlatformStandard { get; init; }
}

public sealed record StageDetailDto
{
    public Guid StageId { get; init; }
    public int SequenceNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? CustomerGoal { get; init; }
    public string? ExpectedEmotion { get; init; }
    public string? DurationHint { get; init; }
    public IReadOnlyList<TouchpointDetailDto> Touchpoints { get; init; } = [];
}

public sealed record JourneyDetailResponse
{
    public Guid JourneyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string JourneyType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<PersonaBindingDto> PersonaBindings { get; init; } = [];
    public IReadOnlyList<StageDetailDto> Stages { get; init; } = [];
    public DateTime UpdatedAt { get; init; }
    public Guid UpdatedBy { get; init; }
}

public sealed record UpdateJourneyResponse
{
    public Guid JourneyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public sealed record StatusChangeResponse
{
    public Guid JourneyId { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime UpdatedAt { get; init; }
}

public sealed record UpdatedAtResponse
{
    public DateTime UpdatedAt { get; init; }
    public Guid UpdatedByUserId { get; init; }
    public string UpdatedByName { get; init; } = string.Empty;
}

public sealed record ApiErrorDetail
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed record ApiErrorResponse
{
    public required ApiErrorDetail Error { get; init; }
}
