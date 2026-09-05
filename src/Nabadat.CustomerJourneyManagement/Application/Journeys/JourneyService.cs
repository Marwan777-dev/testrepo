using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Journeys;

/// <summary>
/// The journey aggregate application service (T024 / US-1). Owns the create / read / list /
/// update operations for the <see cref="Journey"/> root defined in <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description>
///     <b>Create</b> — validates the name (required, ≤255 chars) and the case-insensitive,
///     Archived-excluding uniqueness rule via <see cref="IJourneyNameUniquenessValidator"/>
///     <i>before</i> any write, persists a <c>Draft</c> journey, and publishes
///     <c>journey.created</c> in the same transaction (FR-015).
///   </description></item>
///   <item><description>
///     <b>Get</b> — returns the full journey tree (journey → stages → touchpoints). KPI bindings
///     are layered on in US-2; until then a touchpoint's measured/unmeasured state is derived
///     downstream, so the tree carries only the structural rows.
///   </description></item>
///   <item><description><b>List</b> — cursor-paginated (API-04), optionally status-filtered.</description></item>
///   <item><description>
///     <b>Update</b> — metadata edit guarded by the <c>Archived</c>-immutable rule
///     (<c>journey.archived_immutable</c>); re-runs the uniqueness check excluding the journey
///     itself, then persists and publishes <c>journey.updated</c> atomically.
///   </description></item>
/// </list>
/// The lifecycle status machine lives in <see cref="JourneyStatusTransitionService"/> (T022); this
/// service never changes <c>status</c> except by setting the initial <c>Draft</c> on create.
/// Validation runs before the transaction opens, so every rejection path writes nothing.
/// </summary>
public sealed class JourneyService
{
    private const int MaxNameLength = 255;

    /// <summary>Error code shared with the personas API for "referenced persona is not Active".</summary>
    private const string InvalidPersonaCode = "journey.invalid_persona";

    private readonly IJourneyDataService _journeys;
    private readonly IStageDataService _stages;
    private readonly ITouchpointDataService _touchpoints;
    private readonly IPersonaDataService _personas;
    private readonly IJourneyNameUniquenessValidator _uniqueness;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public JourneyService(
        IJourneyDataService journeys,
        IStageDataService stages,
        ITouchpointDataService touchpoints,
        IPersonaDataService personas,
        IJourneyNameUniquenessValidator uniqueness,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _journeys = journeys;
        _stages = stages;
        _touchpoints = touchpoints;
        _personas = personas;
        _uniqueness = uniqueness;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Creates a <c>Draft</c> journey on behalf of <paramref name="actor"/>. Returns the new
    /// journey id on success (journey row + <c>journey.created</c> event committed in one tx), or a
    /// failure carrying <c>journey.validation_error</c> (blank/over-length name or blank type) or
    /// <c>journey.name_conflict</c> (name already held by a non-Archived journey). No write occurs
    /// on any failure path.
    /// </summary>
    public async Task<ServiceResult<Guid>> CreateJourneyAsync(
        CreateJourneyRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        if (ValidateMetadata(name, request.JourneyType) is { } validationError)
        {
            return ServiceResult<Guid>.Failure(validationError.Code, validationError.Message);
        }

        var uniqueness = await _uniqueness.ValidateAsync(name, excludeJourneyId: null, ct);
        if (!uniqueness.IsSuccess)
        {
            return ServiceResult<Guid>.Failure(uniqueness.Error!.Code, uniqueness.Error.Message);
        }

        var now = _time.GetUtcNow();
        var journey = new Journey
        {
            JourneyId = Guid.NewGuid(),
            Name = name,
            Description = request.Description,
            JourneyType = request.JourneyType.Trim(),
            Status = JourneyStatus.Draft.ToString(),
            CreatedBy = actor.UserId,
            // UpdatedBy stays null until the first edit (entity contract); UpdatedAt mirrors
            // CreatedAt so the concurrent-edit poll has a non-null baseline from creation.
            UpdatedBy = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            await _journeys.CreateAsync(journey, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyCreated(
                    actor.UserId,
                    actor.Persona,
                    journey.JourneyId,
                    now,
                    actor.CorrelationId,
                    newValue: new { journey.Name, journey.JourneyType, status = journey.Status }),
                ct);
        }, ct);

        return ServiceResult<Guid>.Success(journey.JourneyId);
    }

    /// <summary>
    /// Loads the full journey tree: journey → ordered stages → touchpoints, with each touchpoint's
    /// KPI bindings (and derived measured state) and the journey's bound personas. Returns
    /// <c>journey.not_found</c> when the id does not exist. Read-only: opens no transaction. KPI
    /// bindings are fetched in one set-based query for the whole journey (no N+1) and grouped by
    /// touchpoint; bound personas in one join.
    /// </summary>
    public async Task<ServiceResult<JourneyTree>> GetJourneyAsync(Guid journeyId, CancellationToken ct = default)
    {
        var journey = await _journeys.GetByIdAsync(journeyId, ct);
        if (journey is null)
        {
            return ServiceResult<JourneyTree>.Failure("journey.not_found", $"Journey {journeyId} does not exist.");
        }

        var stages = await _stages.ListByJourneyAsync(journeyId, ct);

        // One query for every binding in the journey, grouped by touchpoint in memory — a touchpoint
        // absent from the map is unmeasured (empty bindings ⇒ isMeasured:false, FR-008).
        var allBindings = await _touchpoints.ListKpiBindingsByJourneyAsync(journeyId, ct);
        var bindingsByTouchpoint = allBindings
            .GroupBy(b => b.TouchpointId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<KpiBinding>)g.ToList());

        var stageTrees = new List<StageWithTouchpoints>(stages.Count);
        foreach (var stage in stages)
        {
            var touchpoints = await _touchpoints.ListByStageAsync(stage.StageId, ct);
            var touchpointTrees = touchpoints
                .Select(tp => new TouchpointWithBindings
                {
                    Touchpoint = tp,
                    KpiBindings = bindingsByTouchpoint.GetValueOrDefault(tp.TouchpointId) ?? [],
                })
                .ToList();
            stageTrees.Add(new StageWithTouchpoints { Stage = stage, Touchpoints = touchpointTrees });
        }

        var personaBindings = await _personas.ListBoundPersonasAsync(journeyId, ct);

        return ServiceResult<JourneyTree>.Success(new JourneyTree
        {
            Journey = journey,
            Stages = stageTrees,
            PersonaBindings = personaBindings,
        });
    }

    /// <summary>
    /// Cursor-paginated journey list (API-04), optionally filtered by lifecycle
    /// <paramref name="status"/>. The page carries the journey rows plus the opaque next cursor and
    /// total count; the API layer projects each row to the list DTO. Read-only.
    /// </summary>
    public async Task<ServiceResult<RepositoryPage<Journey>>> ListJourneysAsync(
        string? status,
        int pageSize,
        string? pageToken,
        CancellationToken ct = default)
    {
        var page = await _journeys.ListAsync(status, pageSize, pageToken, ct);
        return ServiceResult<RepositoryPage<Journey>>.Success(page);
    }

    /// <summary>
    /// Updates a journey's metadata (name / description / type) on behalf of
    /// <paramref name="actor"/>. Returns the updated journey on success (row + <c>journey.updated</c>
    /// event committed in one tx), or a failure carrying <c>journey.not_found</c>,
    /// <c>journey.archived_immutable</c> (Archived journeys are frozen), <c>journey.validation_error</c>,
    /// or <c>journey.name_conflict</c>. No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult<Journey>> UpdateJourneyAsync(
        Guid journeyId,
        UpdateJourneyRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var journey = await _journeys.GetByIdAsync(journeyId, ct);
        if (journey is null)
        {
            return ServiceResult<Journey>.Failure("journey.not_found", $"Journey {journeyId} does not exist.");
        }

        // Archived is terminal/immutable — metadata edits are rejected (contract: 403). This guard
        // precedes validation and the transaction, so an archived journey is never touched.
        if (string.Equals(journey.Status, JourneyStatus.Archived.ToString(), StringComparison.Ordinal))
        {
            return ServiceResult<Journey>.Failure(
                "journey.archived_immutable",
                "Archived journeys are immutable and cannot be edited.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (ValidateMetadata(name, request.JourneyType) is { } validationError)
        {
            return ServiceResult<Journey>.Failure(validationError.Code, validationError.Message);
        }

        // Exclude the journey itself so a rename to the same (or case-variant) name never conflicts.
        var uniqueness = await _uniqueness.ValidateAsync(name, excludeJourneyId: journeyId, ct);
        if (!uniqueness.IsSuccess)
        {
            return ServiceResult<Journey>.Failure(uniqueness.Error!.Code, uniqueness.Error.Message);
        }

        // Persona-binding reconciliation (US-3, FR-005). request.PersonaIds is the full replacement
        // set; null leaves bindings untouched. Only newly-added personas must be Active — keeping an
        // already-bound persona that has since gone Inactive is allowed, and unbinding is always
        // permitted. Resolve the add/remove deltas and validate the additions BEFORE the transaction
        // opens, so a rejected save writes nothing (matches every other rejection path here).
        IReadOnlyList<Guid> personasToAdd = [];
        IReadOnlyList<Guid> personasToRemove = [];
        if (request.PersonaIds is not null)
        {
            var requested = request.PersonaIds.ToHashSet();
            var current = (await _personas.ListBoundPersonasAsync(journeyId, ct))
                .Select(p => p.PersonaId)
                .ToHashSet();
            personasToAdd = requested.Except(current).ToList();
            personasToRemove = current.Except(requested).ToList();

            foreach (var personaId in personasToAdd)
            {
                var persona = await _personas.GetByIdAsync(personaId, ct);
                if (persona is null
                    || !string.Equals(persona.Status, PersonaStatus.Active.ToString(), StringComparison.Ordinal))
                {
                    return ServiceResult<Journey>.Failure(
                        InvalidPersonaCode,
                        $"Persona {personaId} is not Active and cannot be bound to journey {journeyId}.");
                }
            }
        }

        var now = _time.GetUtcNow();
        var oldValue = new { journey.Name, journey.Description, journey.JourneyType };

        await _db.ExecuteAsync(async () =>
        {
            journey.Name = name;
            journey.Description = request.Description;
            journey.JourneyType = request.JourneyType.Trim();
            journey.UpdatedBy = actor.UserId;
            journey.UpdatedAt = now;
            await _journeys.UpdateAsync(journey, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyUpdated(
                    actor.UserId,
                    actor.Persona,
                    journeyId,
                    now,
                    actor.CorrelationId,
                    newValue: new { journey.Name, journey.Description, journey.JourneyType },
                    oldValue: oldValue),
                ct);

            // Apply the binding deltas on the same transaction so the metadata edit, its audit row,
            // and the binding changes commit or roll back together (FR-015).
            foreach (var personaId in personasToRemove)
            {
                await _personas.RemoveBindingAsync(journeyId, personaId, ct);
            }

            foreach (var personaId in personasToAdd)
            {
                await _personas.AddBindingAsync(
                    new JourneyPersonaBinding { JourneyId = journeyId, PersonaId = personaId, BoundAt = now },
                    ct);
            }
        }, ct);

        return ServiceResult<Journey>.Success(journey);
    }

    /// <summary>
    /// Shared name/type shape check for create and update. Returns the offending
    /// <see cref="Error"/> (code <c>journey.validation_error</c>) or <c>null</c> when valid.
    /// <paramref name="name"/> is expected pre-trimmed.
    /// </summary>
    private static Error? ValidateMetadata(string name, string? journeyType)
    {
        if (name.Length == 0)
        {
            return new Error("journey.validation_error", "Journey name is required.");
        }

        if (name.Length > MaxNameLength)
        {
            return new Error("journey.validation_error", $"Journey name must be {MaxNameLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(journeyType))
        {
            return new Error("journey.validation_error", "Journey type is required.");
        }

        return null;
    }
}

/// <summary>
/// Create-journey input (<c>POST /api/v1/journeys</c>). Persona binding (<c>personaIds</c> in the
/// HTTP contract) is layered on in US-3, so it is intentionally absent here.
/// </summary>
/// <param name="Name">Display name; required, ≤255 chars, case-insensitively unique per tenant.</param>
/// <param name="Description">Optional free-text description.</param>
/// <param name="JourneyType">Required free-form type tag (e.g. <c>Onboarding</c>, <c>Support</c>).</param>
public sealed record CreateJourneyRequest(string Name, string? Description, string JourneyType);

/// <summary>Update-journey input (<c>PUT /api/v1/journeys/{id}</c>); same name/type validation as create.</summary>
/// <param name="Name">New display name; required, ≤255 chars, unique (excluding this journey).</param>
/// <param name="Description">New optional description.</param>
/// <param name="JourneyType">New required type tag.</param>
/// <param name="PersonaIds">
/// Full replacement set of bound persona ids (US-3, FR-005). <c>null</c> leaves bindings unchanged;
/// a (possibly empty) list reconciles bindings to exactly this set. Newly-added personas must be
/// <c>Active</c> or the save fails with <c>journey.invalid_persona</c>.
/// </param>
public sealed record UpdateJourneyRequest(
    string Name,
    string? Description,
    string JourneyType,
    IReadOnlyList<Guid>? PersonaIds = null);

/// <summary>
/// The full journey tree returned by <see cref="JourneyService.GetJourneyAsync"/> — the journey
/// root plus its ordered stages, each with its touchpoints. The API layer projects this to the
/// <c>GET /api/v1/journeys/{id}</c> response.
/// </summary>
public sealed record JourneyTree
{
    public required Journey Journey { get; init; }

    /// <summary>Stages in <c>sequence_number</c> order, each with its touchpoints.</summary>
    public required IReadOnlyList<StageWithTouchpoints> Stages { get; init; }

    /// <summary>Personas currently bound to the journey (empty when none); projected to the
    /// <c>personaBindings</c> array of the <c>GET /api/v1/journeys/{id}</c> response.</summary>
    public IReadOnlyList<Persona> PersonaBindings { get; init; } = [];
}

/// <summary>One stage paired with its touchpoints (each with its KPI bindings), as carried inside a <see cref="JourneyTree"/>.</summary>
public sealed record StageWithTouchpoints
{
    public required Stage Stage { get; init; }

    public required IReadOnlyList<TouchpointWithBindings> Touchpoints { get; init; }
}

/// <summary>
/// One touchpoint paired with its KPI bindings, as carried inside a <see cref="StageWithTouchpoints"/>.
/// An empty <see cref="KpiBindings"/> set means the touchpoint is unmeasured (<c>isMeasured:false</c>, FR-008).
/// </summary>
public sealed record TouchpointWithBindings
{
    public required Touchpoint Touchpoint { get; init; }

    public required IReadOnlyList<KpiBinding> KpiBindings { get; init; }
}
