using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Application.Limits;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Touchpoints;

/// <summary>
/// The touchpoint application service (T026 / US-1). Owns add / update / delete / read for the
/// <see cref="Touchpoint"/> children of a stage, per <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description>
///     <b>Add</b> — enforces the per-tenant touchpoint-per-stage limit
///     (<see cref="IJourneyLimitProvider"/>), persists the touchpoint with its channel set, and
///     publishes <c>journey.touchpoint.added</c> in the same transaction (FR-015).
///   </description></item>
///   <item><description>
///     <b>Update</b> — edits touchpoint metadata (no dedicated M-17 event in the registry — there
///     is only <c>added</c>/<c>removed</c>); bumps <c>updated_at</c>.
///   </description></item>
///   <item><description>
///     <b>Delete</b> — removes the touchpoint (its <c>kpi_bindings</c> cascade) and publishes
///     <c>journey.touchpoint.removed</c> in the same transaction.
///   </description></item>
///   <item><description>
///     <b>Get</b> — returns the touchpoint together with its derived
///     <see cref="TouchpointView.IsMeasured"/> flag (FR-008): a touchpoint with no KPI bindings is
///     "unmeasured" (<c>isMeasured: false</c>) and excluded from score computation.
///   </description></item>
/// </list>
/// Every mutation requires a non-Archived parent journey (<c>journey.archived_immutable</c>); all
/// guards run before the transaction opens, so a rejected operation writes nothing.
/// </summary>
public sealed class TouchpointService
{
    private readonly ITouchpointDataService _touchpoints;
    private readonly IStageDataService _stages;
    private readonly IJourneyDataService _journeys;
    private readonly IJourneyLimitProvider _limits;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public TouchpointService(
        ITouchpointDataService touchpoints,
        IStageDataService stages,
        IJourneyDataService journeys,
        IJourneyLimitProvider limits,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _touchpoints = touchpoints;
        _stages = stages;
        _journeys = journeys;
        _limits = limits;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Appends a touchpoint to <paramref name="stageId"/>. Returns the created touchpoint on success
    /// (row + <c>journey.touchpoint.added</c> committed in one tx), or a failure carrying
    /// <c>journey.validation_error</c> (blank name), <c>journey.stage_not_found</c>,
    /// <c>journey.not_found</c>, <c>journey.archived_immutable</c>, or
    /// <c>journey.touchpoint_limit_reached</c>. No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult<Touchpoint>> AddTouchpointAsync(
        Guid stageId,
        AddTouchpointRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<Touchpoint>.Failure("journey.validation_error", "Touchpoint name is required.");
        }

        var guard = await LoadWritableParentAsync(stageId, ct);
        if (guard.Error is { } parentError)
        {
            return ServiceResult<Touchpoint>.Failure(parentError.Code, parentError.Message);
        }

        var limits = await _limits.GetLimitsAsync(ct);
        var touchpointCount = await _touchpoints.CountByStageAsync(stageId, ct);
        if (touchpointCount >= limits.MaxTouchpointsPerStage)
        {
            return ServiceResult<Touchpoint>.Failure(
                "journey.touchpoint_limit_reached",
                $"This stage already has the maximum of {limits.MaxTouchpointsPerStage} touchpoints.");
        }

        var now = _time.GetUtcNow();
        var touchpoint = new Touchpoint
        {
            TouchpointId = Guid.NewGuid(),
            StageId = stageId,
            Name = name,
            Description = request.Description,
            Channels = request.Channels ?? [],
            Importance = string.IsNullOrWhiteSpace(request.Importance) ? "Medium" : request.Importance,
            IsMot = request.IsMot,
            IsMandatory = request.IsMandatory,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            await _touchpoints.CreateAsync(touchpoint, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyTouchpointAdded(
                    actor.UserId,
                    actor.Persona,
                    touchpoint.TouchpointId,
                    now,
                    actor.CorrelationId,
                    newValue: new { stageId, touchpoint.Name, touchpoint.Channels, touchpoint.Importance }),
                ct);
        }, ct);

        return ServiceResult<Touchpoint>.Success(touchpoint);
    }

    /// <summary>
    /// Updates a touchpoint's metadata. Returns the updated touchpoint, or a failure carrying
    /// <c>journey.touchpoint_not_found</c>, <c>journey.stage_not_found</c>, <c>journey.not_found</c>,
    /// <c>journey.archived_immutable</c>, or <c>journey.validation_error</c>. Touchpoint metadata
    /// edits have no registered M-17 event, so none is published; the row's <c>updated_at</c> is
    /// bumped.
    /// </summary>
    public async Task<ServiceResult<Touchpoint>> UpdateTouchpointAsync(
        Guid touchpointId,
        UpdateTouchpointRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var touchpoint = await _touchpoints.GetByIdAsync(touchpointId, ct);
        if (touchpoint is null)
        {
            return ServiceResult<Touchpoint>.Failure("journey.touchpoint_not_found", $"Touchpoint {touchpointId} does not exist.");
        }

        var guard = await LoadWritableParentAsync(touchpoint.StageId, ct);
        if (guard.Error is { } parentError)
        {
            return ServiceResult<Touchpoint>.Failure(parentError.Code, parentError.Message);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return ServiceResult<Touchpoint>.Failure("journey.validation_error", "Touchpoint name is required.");
        }

        await _db.ExecuteAsync(async () =>
        {
            touchpoint.Name = name;
            touchpoint.Description = request.Description;
            touchpoint.Channels = request.Channels ?? [];
            touchpoint.Importance = string.IsNullOrWhiteSpace(request.Importance) ? "Medium" : request.Importance;
            touchpoint.IsMot = request.IsMot;
            touchpoint.IsMandatory = request.IsMandatory;
            touchpoint.UpdatedAt = _time.GetUtcNow();
            await _touchpoints.UpdateAsync(touchpoint, ct);
        }, ct);

        return ServiceResult<Touchpoint>.Success(touchpoint);
    }

    /// <summary>
    /// Deletes a touchpoint (its <c>kpi_bindings</c> cascade). Returns success once the row is
    /// removed and <c>journey.touchpoint.removed</c> is published (one tx), or a failure carrying
    /// <c>journey.touchpoint_not_found</c>, <c>journey.stage_not_found</c>, <c>journey.not_found</c>,
    /// or <c>journey.archived_immutable</c>. No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult> DeleteTouchpointAsync(Guid touchpointId, ActorContext actor, CancellationToken ct = default)
    {
        var touchpoint = await _touchpoints.GetByIdAsync(touchpointId, ct);
        if (touchpoint is null)
        {
            return ServiceResult.Failure("journey.touchpoint_not_found", $"Touchpoint {touchpointId} does not exist.");
        }

        var guard = await LoadWritableParentAsync(touchpoint.StageId, ct);
        if (guard.Error is { } parentError)
        {
            return ServiceResult.Failure(parentError.Code, parentError.Message);
        }

        var now = _time.GetUtcNow();
        await _db.ExecuteAsync(async () =>
        {
            await _touchpoints.DeleteAsync(touchpointId, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyTouchpointRemoved(
                    actor.UserId,
                    actor.Persona,
                    touchpointId,
                    now,
                    actor.CorrelationId,
                    oldValue: new { touchpoint.StageId, touchpoint.Name }),
                ct);
        }, ct);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Reads a single touchpoint with its derived <see cref="TouchpointView.IsMeasured"/> flag.
    /// Returns the view on success, or <c>journey.touchpoint_not_found</c>. The flag is
    /// <c>false</c> until the touchpoint carries at least one KPI binding (FR-008). This is a read —
    /// no transaction and no actor required.
    /// </summary>
    public async Task<ServiceResult<TouchpointView>> GetTouchpointAsync(Guid touchpointId, CancellationToken ct = default)
    {
        var touchpoint = await _touchpoints.GetByIdAsync(touchpointId, ct);
        if (touchpoint is null)
        {
            return ServiceResult<TouchpointView>.Failure("journey.touchpoint_not_found", $"Touchpoint {touchpointId} does not exist.");
        }

        var isMeasured = await _touchpoints.HasKpiBindingsAsync(touchpointId, ct);
        return ServiceResult<TouchpointView>.Success(new TouchpointView(touchpoint, isMeasured));
    }

    /// <summary>
    /// Loads the touchpoint's parent stage and journey and applies the existence + Archived-immutable
    /// guards shared by every mutating touchpoint operation. Returns the stage on success, or the
    /// failing <see cref="Error"/> (<c>journey.stage_not_found</c> / <c>journey.not_found</c> /
    /// <c>journey.archived_immutable</c>).
    /// </summary>
    private async Task<(Stage? Stage, Error? Error)> LoadWritableParentAsync(Guid stageId, CancellationToken ct)
    {
        var stage = await _stages.GetByIdAsync(stageId, ct);
        if (stage is null)
        {
            return (null, new Error("journey.stage_not_found", $"Stage {stageId} does not exist."));
        }

        var journey = await _journeys.GetByIdAsync(stage.JourneyId, ct);
        if (journey is null)
        {
            return (null, new Error("journey.not_found", $"Journey {stage.JourneyId} does not exist."));
        }

        if (string.Equals(journey.Status, JourneyStatus.Archived.ToString(), StringComparison.Ordinal))
        {
            return (null, new Error("journey.archived_immutable", "Archived journeys are immutable and cannot be edited."));
        }

        return (stage, null);
    }
}

/// <summary>
/// Add-touchpoint input (<c>POST /api/v1/stages/{id}/touchpoints</c>). Only <see cref="Name"/> is
/// required; a touchpoint starts unmeasured (no KPI bindings) until they are configured (US-2).
/// </summary>
/// <param name="Name">Touchpoint name; required.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Channels">Channel codes, e.g. <c>{IVR, Web}</c>; defaults to empty.</param>
/// <param name="Importance"><c>Low</c> | <c>Medium</c> | <c>High</c> | <c>Critical</c>; defaults to <c>Medium</c>.</param>
/// <param name="IsMot">Moment-of-Truth flag.</param>
/// <param name="IsMandatory">When true, the touchpoint is always included in score calculation.</param>
public sealed record AddTouchpointRequest(
    string Name,
    string? Description = null,
    string[]? Channels = null,
    string Importance = "Medium",
    bool IsMot = false,
    bool IsMandatory = false);

/// <summary>Update-touchpoint input (<c>PUT /api/v1/touchpoints/{id}</c>); same shape as add.</summary>
/// <param name="Name">New touchpoint name; required.</param>
/// <param name="Description">New optional description.</param>
/// <param name="Channels">New channel set; defaults to empty.</param>
/// <param name="Importance">New importance; defaults to <c>Medium</c>.</param>
/// <param name="IsMot">New Moment-of-Truth flag.</param>
/// <param name="IsMandatory">New mandatory flag.</param>
public sealed record UpdateTouchpointRequest(
    string Name,
    string? Description = null,
    string[]? Channels = null,
    string Importance = "Medium",
    bool IsMot = false,
    bool IsMandatory = false);

/// <summary>
/// A touchpoint paired with its derived <see cref="IsMeasured"/> flag (FR-008). A touchpoint with
/// no KPI bindings is unmeasured (<c>isMeasured: false</c>) — surfaced read-only on the journey tree
/// and visually flagged in the UI, and excluded from score computation.
/// </summary>
/// <param name="Touchpoint">The touchpoint entity.</param>
/// <param name="IsMeasured">True when the touchpoint carries at least one KPI binding.</param>
public sealed record TouchpointView(Touchpoint Touchpoint, bool IsMeasured);
