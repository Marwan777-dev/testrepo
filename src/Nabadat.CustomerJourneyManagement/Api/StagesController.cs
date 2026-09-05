using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nabadat.UserManagement.Api.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Journeys;
using Nabadat.CustomerJourneyManagement.Application.Stages;

namespace Nabadat.CustomerJourneyManagement.Api;

/// <summary>
/// Stage CRUD and reorder endpoints (T029 / US-1). Implements the five stage operations per
/// <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description><c>POST /api/v1/journeys/{journeyId}/stages</c> — append a stage (P-01/P-02, <c>journey.write</c>)</description></item>
///   <item><description><c>GET /api/v1/journeys/{journeyId}/stages</c> — list stages ordered by sequence with touchpoint counts (<c>journey.read</c>)</description></item>
///   <item><description><c>PUT /api/v1/journeys/{journeyId}/stages/{stageId}</c> — update stage metadata (<c>journey.write</c>)</description></item>
///   <item><description><c>DELETE /api/v1/journeys/{journeyId}/stages/{stageId}</c> — delete a stage, blocked when it owns touchpoints (<c>journey.write</c>)</description></item>
///   <item><description><c>PUT /api/v1/journeys/{journeyId}/stages/reorder</c> — replace the full stage ordering (<c>journey.write</c>)</description></item>
/// </list>
/// Authorization is declared per endpoint (the contract assigns different permissions to reads vs
/// writes); the tenant is resolved from the JWT (API-02) by <c>M10AuthenticationMiddleware</c> and
/// every non-2xx response follows the API-05 error envelope. Each business failure code from
/// <see cref="StageService"/> is mapped to its contract HTTP status below.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/journeys/{journeyId}/stages")]
public sealed class StagesController : ControllerBase
{
    private readonly StageService _stages;
    private readonly JourneyService _journeys;
    private readonly ISessionContextAccessor _sessionAccessor;
    private readonly TimeProvider _time;

    public StagesController(
        StageService stages,
        JourneyService journeys,
        ISessionContextAccessor sessionAccessor,
        TimeProvider time)
    {
        _stages = stages;
        _journeys = journeys;
        _sessionAccessor = sessionAccessor;
        _time = time;
    }

    /// <summary>
    /// POST /api/v1/journeys/{journeyId}/stages — Appends a stage at the end of the journey's
    /// sequence. Required permission: journey.write.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AddStageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddStageResponse>> AddStage(
        [FromRoute] Guid journeyId,
        [FromBody] AddStageRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new AddStageRequest(
            request.Name,
            request.Description,
            request.CustomerGoal,
            request.ExpectedEmotion,
            request.DurationHint);

        var result = await _stages.AddStageAsync(journeyId, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var stage = result.Value!;
        return CreatedAtAction(
            nameof(ListStages),
            new { journeyId },
            new AddStageResponse
            {
                StageId = stage.StageId,
                SequenceNumber = stage.SequenceNumber,
                CreatedAt = stage.CreatedAt.UtcDateTime
            });
    }

    /// <summary>
    /// GET /api/v1/journeys/{journeyId}/stages — Returns the journey's stages ordered by
    /// sequence_number, each with its touchpoint count. Required permission: journey.read.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(StageListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StageListResponse>> ListStages(
        [FromRoute] Guid journeyId,
        CancellationToken ct = default)
    {
        var result = await _journeys.GetJourneyAsync(journeyId, ct);
        if (!result.IsSuccess)
        {
            return NotFound(Envelope(result.Error!));
        }

        var stages = result.Value!.Stages
            .Select(s => new StageSummaryDto
            {
                StageId = s.Stage.StageId,
                SequenceNumber = s.Stage.SequenceNumber,
                Name = s.Stage.Name,
                TouchpointCount = s.Touchpoints.Count
            })
            .ToList();

        return Ok(new StageListResponse { Stages = stages });
    }

    /// <summary>
    /// PUT /api/v1/journeys/{journeyId}/stages/{stageId} — Updates stage metadata.
    /// Required permission: journey.write.
    /// </summary>
    [HttpPut("{stageId:guid}")]
    [ProducesResponseType(typeof(UpdateStageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UpdateStageResponse>> UpdateStage(
        [FromRoute] Guid journeyId,
        [FromRoute] Guid stageId,
        [FromBody] UpdateStageRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var serviceRequest = new UpdateStageRequest(
            request.Name,
            request.Description,
            request.CustomerGoal,
            request.ExpectedEmotion,
            request.DurationHint);

        var result = await _stages.UpdateStageAsync(stageId, serviceRequest, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        var stage = result.Value!;
        return Ok(new UpdateStageResponse
        {
            StageId = stage.StageId,
            UpdatedAt = stage.UpdatedAt.UtcDateTime
        });
    }

    /// <summary>
    /// DELETE /api/v1/journeys/{journeyId}/stages/{stageId} — Deletes a stage. Fails with
    /// journey.stage_has_touchpoints (409) when the stage still owns touchpoints.
    /// Required permission: journey.write.
    /// </summary>
    [HttpDelete("{stageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteStage(
        [FromRoute] Guid journeyId,
        [FromRoute] Guid stageId,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var result = await _stages.DeleteStageAsync(stageId, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        return NoContent();
    }

    /// <summary>
    /// PUT /api/v1/journeys/{journeyId}/stages/reorder — Replaces the journey's stage ordering with
    /// the supplied complete sequence of stage ids. Required permission: journey.write.
    /// </summary>
    [HttpPut("reorder")]
    [ProducesResponseType(typeof(ReorderStagesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ReorderStagesResponse>> ReorderStages(
        [FromRoute] Guid journeyId,
        [FromBody] ReorderStagesRequestDto request,
        CancellationToken ct = default)
    {
        var actor = CurrentActor();

        var orderedStageIds = request.StageIds ?? [];
        var result = await _stages.ReorderStagesAsync(journeyId, orderedStageIds, actor, ct);
        if (!result.IsSuccess)
        {
            return MapError(result.Error!);
        }

        return Ok(new ReorderStagesResponse
        {
            JourneyId = journeyId,
            ReorderedAt = _time.GetUtcNow().UtcDateTime
        });
    }

    /// <summary>
    /// Resolves the authenticated caller into an <see cref="ActorContext"/>. Returns <c>false</c>
    /// with a 401 API-05 envelope in <paramref name="unauthorized"/> when no session is present.
    /// </summary>
    private ActorContext CurrentActor()
    {
        var session = _sessionAccessor.Current!;
        return new ActorContext(session.UserId, session.Persona, HttpContext.CorrelationId());
    }

    /// <summary>
    /// Maps a <see cref="StageService"/> failure <see cref="Error"/> onto the HTTP status defined for
    /// it in <c>contracts/journeys-api.md</c>, wrapped in the API-05 envelope. Unknown codes default
    /// to 422 (validation), matching <see cref="JourneysController"/>'s convention.
    /// </summary>
    private ObjectResult MapError(Error error) => error.Code switch
    {
        "journey.not_found" => NotFound(Envelope(error)),
        "journey.stage_not_found" => NotFound(Envelope(error)),
        "journey.archived_immutable" => StatusCode(StatusCodes.Status403Forbidden, Envelope(error)),
        "journey.stage_has_touchpoints" => Conflict(Envelope(error)),
        _ => UnprocessableEntity(Envelope(error))
    };

    /// <summary>Wraps an <see cref="Error"/> in the API-05 response envelope.</summary>
    private static ApiErrorResponse Envelope(Error error) => new()
    {
        Error = new ApiErrorDetail { Code = error.Code, Message = error.Message }
    };
}

/// <summary>API request/response DTOs for stage endpoints.</summary>

public sealed record AddStageRequestDto(
    string Name,
    string? Description = null,
    string? CustomerGoal = null,
    string? ExpectedEmotion = null,
    string? DurationHint = null);

public sealed record UpdateStageRequestDto(
    string Name,
    string? Description = null,
    string? CustomerGoal = null,
    string? ExpectedEmotion = null,
    string? DurationHint = null);

public sealed record ReorderStagesRequestDto(IReadOnlyList<Guid>? StageIds);

public sealed record AddStageResponse
{
    public Guid StageId { get; init; }
    public int SequenceNumber { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record StageSummaryDto
{
    public Guid StageId { get; init; }
    public int SequenceNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public int TouchpointCount { get; init; }
}

public sealed record StageListResponse
{
    public required IReadOnlyList<StageSummaryDto> Stages { get; init; }
}

public sealed record UpdateStageResponse
{
    public Guid StageId { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed record ReorderStagesResponse
{
    public Guid JourneyId { get; init; }
    public DateTime ReorderedAt { get; init; }
}
