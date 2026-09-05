using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Limits;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Stages;

/// <summary>
/// The stage application service (T025 / US-1). Owns add / update / delete / reorder for the
/// <see cref="Stage"/> children of a journey, per <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description>
///     <b>Add</b> — enforces the per-tenant stage limit (<see cref="IJourneyLimitProvider"/>),
///     appends at <c>max(sequence_number) + 1</c>, and publishes <c>journey.stage.added</c> in the
///     same transaction (FR-015).
///   </description></item>
///   <item><description><b>Update</b> — edits stage metadata (no dedicated M-17 event in the registry).</description></item>
///   <item><description>
///     <b>Delete</b> — blocked while the stage still owns touchpoints
///     (<c>journey.stage_has_touchpoints</c>); otherwise removes the row and publishes
///     <c>journey.stage.removed</c>.
///   </description></item>
///   <item><description>
///     <b>Reorder</b> — validates the new order is a permutation of the journey's stages
///     (<see cref="StageReorderService"/>) then persists it atomically via the repository's
///     two-phase reorder. No M-17 event (none registered for reorder).
///   </description></item>
/// </list>
/// Every mutation requires a non-Archived parent journey (<c>journey.archived_immutable</c>);
/// all guards run before the transaction opens, so a rejected operation writes nothing.
/// </summary>
public sealed class StageService
{
    private readonly IJourneyDataService _journeys;
    private readonly IStageDataService _stages;
    private readonly ITouchpointDataService _touchpoints;
    private readonly IJourneyLimitProvider _limits;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public StageService(
        IJourneyDataService journeys,
        IStageDataService stages,
        ITouchpointDataService touchpoints,
        IJourneyLimitProvider limits,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _journeys = journeys;
        _stages = stages;
        _touchpoints = touchpoints;
        _limits = limits;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Appends a stage to <paramref name="journeyId"/> at the next sequence position. Returns the
    /// created stage on success (row + <c>journey.stage.added</c> committed in one tx), or a failure
    /// carrying <c>journey.not_found</c>, <c>journey.archived_immutable</c>,
    /// <c>journey.validation_error</c> (blank name), or <c>journey.stage_limit_reached</c>. No write
    /// occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult<Stage>> AddStageAsync(
        Guid journeyId,
        AddStageRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<Stage>.Failure("journey.validation_error", "Stage name is required.");
        }

        var guard = await LoadWritableJourneyAsync(journeyId, ct);
        if (guard.Error is { } journeyError)
        {
            return ServiceResult<Stage>.Failure(journeyError.Code, journeyError.Message);
        }

        var limits = await _limits.GetLimitsAsync(ct);
        var stageCount = await _stages.CountByJourneyAsync(journeyId, ct);
        if (stageCount >= limits.MaxStagesPerJourney)
        {
            return ServiceResult<Stage>.Failure(
                "journey.stage_limit_reached",
                $"This journey already has the maximum of {limits.MaxStagesPerJourney} stages.");
        }

        var nextSequence = await _stages.GetMaxSequenceNumberAsync(journeyId, ct) + 1;
        var now = _time.GetUtcNow();
        var stage = new Stage
        {
            StageId = Guid.NewGuid(),
            JourneyId = journeyId,
            SequenceNumber = nextSequence,
            Name = name,
            Description = request.Description,
            CustomerGoal = request.CustomerGoal,
            ExpectedEmotion = request.ExpectedEmotion,
            DurationHint = request.DurationHint,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            await _stages.CreateAsync(stage, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyStageAdded(
                    actor.UserId,
                    actor.Persona,
                    stage.StageId,
                    now,
                    actor.CorrelationId,
                    newValue: new { journeyId, stage.Name, stage.SequenceNumber }),
                ct);
        }, ct);

        return ServiceResult<Stage>.Success(stage);
    }

    /// <summary>
    /// Updates a stage's metadata. Returns the updated stage, or a failure carrying
    /// <c>journey.stage_not_found</c>, <c>journey.archived_immutable</c>, or
    /// <c>journey.validation_error</c>. Stage metadata edits have no registered M-17 event, so none
    /// is published; the row's <c>updated_at</c> is bumped.
    /// </summary>
    public async Task<ServiceResult<Stage>> UpdateStageAsync(
        Guid stageId,
        UpdateStageRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stage = await _stages.GetByIdAsync(stageId, ct);
        if (stage is null)
        {
            return ServiceResult<Stage>.Failure("journey.stage_not_found", $"Stage {stageId} does not exist.");
        }

        var guard = await LoadWritableJourneyAsync(stage.JourneyId, ct);
        if (guard.Error is { } journeyError)
        {
            return ServiceResult<Stage>.Failure(journeyError.Code, journeyError.Message);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<Stage>.Failure("journey.validation_error", "Stage name is required.");
        }

        await _db.ExecuteAsync(async () =>
        {
            stage.Name = name;
            stage.Description = request.Description;
            stage.CustomerGoal = request.CustomerGoal;
            stage.ExpectedEmotion = request.ExpectedEmotion;
            stage.DurationHint = request.DurationHint;
            stage.UpdatedAt = _time.GetUtcNow();
            await _stages.UpdateAsync(stage, ct);
        }, ct);

        return ServiceResult<Stage>.Success(stage);
    }

    /// <summary>
    /// Deletes a stage. Returns success once the row is removed and <c>journey.stage.removed</c> is
    /// published (one tx), or a failure carrying <c>journey.stage_not_found</c>,
    /// <c>journey.archived_immutable</c>, or <c>journey.stage_has_touchpoints</c> (the stage still
    /// owns touchpoints — delete or reassign them first). No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult> DeleteStageAsync(Guid stageId, ActorContext actor, CancellationToken ct = default)
    {
        var stage = await _stages.GetByIdAsync(stageId, ct);
        if (stage is null)
        {
            return ServiceResult.Failure("journey.stage_not_found", $"Stage {stageId} does not exist.");
        }

        var guard = await LoadWritableJourneyAsync(stage.JourneyId, ct);
        if (guard.Error is { } journeyError)
        {
            return ServiceResult.Failure(journeyError.Code, journeyError.Message);
        }

        var touchpointCount = await _touchpoints.CountByStageAsync(stageId, ct);
        if (touchpointCount > 0)
        {
            return ServiceResult.Failure(
                "journey.stage_has_touchpoints",
                "This stage still contains touchpoints; delete or reassign them before removing the stage.");
        }

        var now = _time.GetUtcNow();
        await _db.ExecuteAsync(async () =>
        {
            await _stages.DeleteAsync(stageId, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyStageRemoved(
                    actor.UserId,
                    actor.Persona,
                    stageId,
                    now,
                    actor.CorrelationId,
                    oldValue: new { stage.JourneyId, stage.Name, stage.SequenceNumber }),
                ct);
        }, ct);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Replaces the journey's stage ordering with <paramref name="orderedStageIds"/> (the complete
    /// new sequence). Returns success once the new order is persisted, or a failure carrying
    /// <c>journey.not_found</c>, <c>journey.archived_immutable</c>, or <c>journey.invalid_stage_order</c>
    /// (the supplied ids are not a permutation of the journey's stages). The persistence is the
    /// repository's two-phase reorder, run inside one transaction so the unique
    /// <c>(journey_id, sequence_number)</c> index is never transiently violated. No M-17 event is
    /// registered for reorder, so none is published.
    /// </summary>
    public async Task<ServiceResult> ReorderStagesAsync(
        Guid journeyId,
        IReadOnlyList<Guid> orderedStageIds,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orderedStageIds);

        var guard = await LoadWritableJourneyAsync(journeyId, ct);
        if (guard.Error is { } journeyError)
        {
            return ServiceResult.Failure(journeyError.Code, journeyError.Message);
        }

        var existing = await _stages.ListByJourneyAsync(journeyId, ct);
        var existingIds = existing.Select(s => s.StageId).ToArray();
        if (StageReorderService.Validate(existingIds, orderedStageIds) is { } orderError)
        {
            return ServiceResult.Failure(orderError.Code, orderError.Message);
        }

        await _db.ExecuteAsync(() => _stages.ReorderAsync(journeyId, orderedStageIds, ct), ct);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Loads the parent journey and applies the existence + Archived-immutable guards shared by
    /// every mutating stage operation. Returns the journey on success, or the failing
    /// <see cref="Error"/> (<c>journey.not_found</c> / <c>journey.archived_immutable</c>).
    /// </summary>
    private async Task<(Journey? Journey, Error? Error)> LoadWritableJourneyAsync(Guid journeyId, CancellationToken ct)
    {
        var journey = await _journeys.GetByIdAsync(journeyId, ct);
        if (journey is null)
        {
            return (null, new Error("journey.not_found", $"Journey {journeyId} does not exist."));
        }

        if (string.Equals(journey.Status, JourneyStatus.Archived.ToString(), StringComparison.Ordinal))
        {
            return (null, new Error("journey.archived_immutable", "Archived journeys are immutable and cannot be edited."));
        }

        return (journey, null);
    }
}

/// <summary>
/// Add-stage input (<c>POST /api/v1/journeys/{id}/stages</c>). Only <see cref="Name"/> is required;
/// the descriptive fields are optional. The stage is always appended at the end of the sequence.
/// </summary>
/// <param name="Name">Stage name; required.</param>
/// <param name="Description">Optional description.</param>
/// <param name="CustomerGoal">Optional — what the customer is trying to achieve in this stage.</param>
/// <param name="ExpectedEmotion">Optional emotion tag (e.g. <c>excited</c>, <c>anxious</c>).</param>
/// <param name="DurationHint">Optional human-readable duration estimate (e.g. <c>2–5 minutes</c>).</param>
public sealed record AddStageRequest(
    string Name,
    string? Description = null,
    string? CustomerGoal = null,
    string? ExpectedEmotion = null,
    string? DurationHint = null);

/// <summary>Update-stage input (<c>PUT /api/v1/journeys/{id}/stages/{stageId}</c>); same shape as add.</summary>
/// <param name="Name">New stage name; required.</param>
/// <param name="Description">New optional description.</param>
/// <param name="CustomerGoal">New optional customer goal.</param>
/// <param name="ExpectedEmotion">New optional emotion tag.</param>
/// <param name="DurationHint">New optional duration estimate.</param>
public sealed record UpdateStageRequest(
    string Name,
    string? Description = null,
    string? CustomerGoal = null,
    string? ExpectedEmotion = null,
    string? DurationHint = null);
