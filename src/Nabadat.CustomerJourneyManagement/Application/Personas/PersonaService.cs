using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Entities;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Personas;

/// <summary>
/// The persona aggregate application service (T064 / US-3). Owns persona CRUD plus the
/// journey-binding guard defined in <c>contracts/personas-api.md</c> / <c>contracts/journeys-api.md</c>:
/// <list type="bullet">
///   <item><description>
///     <b>Create</b> — validates the bilingual name (<c>nameAr</c>/<c>nameEn</c> both required,
///     ≤255 chars) <i>before</i> any write, persists a <c>Draft</c> persona, and publishes
///     <c>persona.created</c> in the same transaction (FR-015).
///   </description></item>
///   <item><description>
///     <b>Read</b> — <see cref="GetPersonaAsync"/> (single) and <see cref="ListPersonasAsync"/>
///     (optionally status-filtered). The journey-builder binding selector reads only bindable
///     personas via <see cref="ListBindablePersonasAsync"/> (= status <c>Active</c>), so non-Active
///     personas never appear as binding candidates (FR-005).
///   </description></item>
///   <item><description>
///     <b>Update</b> — metadata edit guarded by the <c>Archived</c>-immutable rule
///     (<c>persona.archived_immutable</c>, 403); persists and publishes <c>persona.updated</c> atomically.
///   </description></item>
///   <item><description>
///     <b>Bind / unbind</b> — only an <c>Active</c> persona may be bound to a journey; a non-Active
///     (or unknown) persona is rejected with <c>journey.invalid_persona</c> and writes nothing.
///     Unbinding is always permitted (the contract path to free a persona before archiving it).
///   </description></item>
/// </list>
/// Lifecycle <i>status transitions</i> are NOT handled here — they live in the dedicated
/// <see cref="PersonaStatusTransitionService"/> (T063), which the API layer (T071) delegates to;
/// this service never changes <c>status</c> except by setting the initial <c>Draft</c> on create.
/// Hard deletion is unsupported (archiving is terminal), so there is no delete operation.
/// Validation runs before the transaction opens, so every rejection path writes nothing.
/// </summary>
public sealed class PersonaService
{
    private const int MaxNameLength = 255;

    /// <summary>Error code shared by the journeys API for "referenced persona is not Active".</summary>
    private const string InvalidPersonaCode = "journey.invalid_persona";

    private readonly IPersonaDataService _personas;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public PersonaService(
        IPersonaDataService personas,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _personas = personas;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Creates a <c>Draft</c> persona on behalf of <paramref name="actor"/>. Returns the new persona
    /// id on success (persona row + <c>persona.created</c> event committed in one tx), or a failure
    /// carrying <c>persona.validation_error</c> (blank/over-length name). No write occurs on a
    /// validation failure.
    /// </summary>
    public async Task<ServiceResult<Guid>> CreatePersonaAsync(
        CreatePersonaRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nameAr = request.NameAr?.Trim() ?? string.Empty;
        var nameEn = request.NameEn?.Trim() ?? string.Empty;
        if (ValidateNames(nameAr, nameEn) is { } validationError)
        {
            return ServiceResult<Guid>.Failure(validationError.Code, validationError.Message);
        }

        var now = _time.GetUtcNow();
        var persona = new Persona
        {
            PersonaId = Guid.NewGuid(),
            NameAr = nameAr,
            NameEn = nameEn,
            DescriptionAr = request.DescriptionAr,
            DescriptionEn = request.DescriptionEn,
            // New personas always start in Draft (contract: POST /personas → status "Draft").
            Status = PersonaStatus.Draft.ToString(),
            CreatedBy = actor.UserId,
            // UpdatedBy stays null until the first edit; UpdatedAt mirrors CreatedAt so reads have a
            // non-null baseline from creation (matches JourneyService).
            UpdatedBy = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _db.ExecuteAsync(async () =>
        {
            await _personas.CreateAsync(persona, ct);
            await _events.PublishAsync(
                CustomerJourneyManagementEvent.PersonaCreated(
                    actor.UserId,
                    actor.Persona,
                    persona.PersonaId,
                    now,
                    actor.CorrelationId,
                    newValue: new { persona.NameAr, persona.NameEn, status = persona.Status }),
                ct);
        }, ct);

        return ServiceResult<Guid>.Success(persona.PersonaId);
    }

    /// <summary>
    /// Loads a single persona. Returns <c>persona.not_found</c> when the id does not exist.
    /// Read-only: opens no transaction.
    /// </summary>
    public async Task<ServiceResult<Persona>> GetPersonaAsync(Guid personaId, CancellationToken ct = default)
    {
        var persona = await _personas.GetByIdAsync(personaId, ct);
        return persona is null
            ? ServiceResult<Persona>.Failure("persona.not_found", $"Persona {personaId} does not exist.")
            : ServiceResult<Persona>.Success(persona);
    }

    /// <summary>
    /// Lists personas, optionally filtered by lifecycle <paramref name="status"/>. Read-only; the
    /// API layer projects each row (and layers on the journey binding count) for the list response.
    /// </summary>
    public async Task<ServiceResult<IReadOnlyList<Persona>>> ListPersonasAsync(
        string? status,
        CancellationToken ct = default)
    {
        var personas = await _personas.ListAsync(status, ct);
        return ServiceResult<IReadOnlyList<Persona>>.Success(personas);
    }

    /// <summary>
    /// The Active-only binding selector (FR-005): personas eligible to be bound to a journey. Thin
    /// alias over <see cref="ListPersonasAsync"/> with the <c>Active</c> filter, so non-Active
    /// personas never surface as binding candidates.
    /// </summary>
    public Task<ServiceResult<IReadOnlyList<Persona>>> ListBindablePersonasAsync(CancellationToken ct = default)
        => ListPersonasAsync(PersonaStatus.Active.ToString(), ct);

    /// <summary>
    /// The journeys a persona is currently bound to (id + display name), for the persona-detail
    /// <c>journeyBindings</c> array. Read-only: opens no transaction.
    /// </summary>
    public Task<IReadOnlyList<PersonaJourneyBinding>> ListJourneyBindingsAsync(Guid personaId, CancellationToken ct = default)
        => _personas.ListBindingsForPersonaAsync(personaId, ct);

    /// <summary>
    /// Journey-binding counts keyed by persona id (personas with no bindings are absent ⇒ treat as
    /// 0), for the list endpoint's <c>journeyBindingCount</c>. One grouped query, no N+1. Read-only.
    /// </summary>
    public Task<IReadOnlyDictionary<Guid, int>> GetBindingCountsAsync(CancellationToken ct = default)
        => _personas.CountBindingsByPersonaAsync(ct);

    /// <summary>
    /// Updates a persona's metadata (names / descriptions) on behalf of <paramref name="actor"/>.
    /// Returns the updated persona on success (row + <c>persona.updated</c> event committed in one
    /// tx), or a failure carrying <c>persona.not_found</c>, <c>persona.archived_immutable</c>
    /// (Archived personas are frozen), or <c>persona.validation_error</c>. No write occurs on any
    /// failure path.
    /// </summary>
    public async Task<ServiceResult<Persona>> UpdatePersonaAsync(
        Guid personaId,
        UpdatePersonaRequest request,
        ActorContext actor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var persona = await _personas.GetByIdAsync(personaId, ct);
        if (persona is null)
        {
            return ServiceResult<Persona>.Failure("persona.not_found", $"Persona {personaId} does not exist.");
        }

        // Archived is terminal/immutable — metadata edits are rejected (contract: 403). This guard
        // precedes validation and the transaction, so an archived persona is never touched.
        if (string.Equals(persona.Status, PersonaStatus.Archived.ToString(), StringComparison.Ordinal))
        {
            return ServiceResult<Persona>.Failure(
                "persona.archived_immutable",
                "Archived personas are immutable and cannot be edited.");
        }

        var nameAr = request.NameAr?.Trim() ?? string.Empty;
        var nameEn = request.NameEn?.Trim() ?? string.Empty;
        if (ValidateNames(nameAr, nameEn) is { } validationError)
        {
            return ServiceResult<Persona>.Failure(validationError.Code, validationError.Message);
        }

        var now = _time.GetUtcNow();
        var oldValue = new { persona.NameAr, persona.NameEn, persona.DescriptionAr, persona.DescriptionEn };

        await _db.ExecuteAsync(async () =>
        {
            persona.NameAr = nameAr;
            persona.NameEn = nameEn;
            persona.DescriptionAr = request.DescriptionAr;
            persona.DescriptionEn = request.DescriptionEn;
            persona.UpdatedBy = actor.UserId;
            persona.UpdatedAt = now;
            await _personas.UpdateAsync(persona, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.PersonaUpdated(
                    actor.UserId,
                    actor.Persona,
                    personaId,
                    now,
                    actor.CorrelationId,
                    newValue: new { persona.NameAr, persona.NameEn, persona.DescriptionAr, persona.DescriptionEn },
                    oldValue: oldValue),
                ct);
        }, ct);

        return ServiceResult<Persona>.Success(persona);
    }

    /// <summary>
    /// Binds <paramref name="personaId"/> to <paramref name="journeyId"/> on behalf of
    /// <paramref name="actor"/>. Only an <c>Active</c> persona may be bound (FR-005); a non-Active or
    /// unknown persona is rejected with <c>journey.invalid_persona</c> and writes nothing. No M-17
    /// event is defined for a binding, so the write is a single insert inside the unit of work.
    /// </summary>
    public async Task<ServiceResult> BindPersonaToJourneyAsync(
        Guid journeyId,
        Guid personaId,
        ActorContext actor,
        CancellationToken ct = default)
    {
        var persona = await _personas.GetByIdAsync(personaId, ct);
        if (persona is null
            || !string.Equals(persona.Status, PersonaStatus.Active.ToString(), StringComparison.Ordinal))
        {
            return ServiceResult.Failure(
                InvalidPersonaCode,
                $"Persona {personaId} is not Active and cannot be bound to a journey.");
        }

        var binding = new JourneyPersonaBinding
        {
            JourneyId = journeyId,
            PersonaId = personaId,
            BoundAt = _time.GetUtcNow(),
        };

        // Single write — the data service's own SaveChanges is already atomic; no explicit tx needed.
        await _personas.AddBindingAsync(binding, ct);

        return ServiceResult.Success();
    }

    /// <summary>
    /// Removes the <paramref name="journeyId"/>↔<paramref name="personaId"/> binding. Always
    /// permitted (contract) — the path a caller takes to free a persona before archiving it. Idempotent:
    /// removing a non-existent binding succeeds.
    /// </summary>
    public async Task<ServiceResult> UnbindPersonaFromJourneyAsync(
        Guid journeyId,
        Guid personaId,
        CancellationToken ct = default)
    {
        // Single write — the data service's own SaveChanges is already atomic; no explicit tx needed.
        await _personas.RemoveBindingAsync(journeyId, personaId, ct);
        return ServiceResult.Success();
    }

    /// <summary>
    /// Shared bilingual-name shape check for create and update. Returns the offending
    /// <see cref="Error"/> (code <c>persona.validation_error</c>) or <c>null</c> when valid.
    /// Both names are expected pre-trimmed.
    /// </summary>
    private static Error? ValidateNames(string nameAr, string nameEn)
    {
        if (nameAr.Length == 0 || nameEn.Length == 0)
        {
            return new Error("persona.validation_error", "Persona nameAr and nameEn are both required.");
        }

        if (nameAr.Length > MaxNameLength || nameEn.Length > MaxNameLength)
        {
            return new Error("persona.validation_error", $"Persona names must be {MaxNameLength} characters or fewer.");
        }

        return null;
    }
}

/// <summary>
/// Create-persona input (<c>POST /api/v1/personas</c>).
/// </summary>
/// <param name="NameAr">Arabic label (فصحى); required, ≤255 chars.</param>
/// <param name="NameEn">English label; required, ≤255 chars.</param>
/// <param name="DescriptionAr">Optional Arabic description.</param>
/// <param name="DescriptionEn">Optional English description.</param>
public sealed record CreatePersonaRequest(string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn);

/// <summary>Update-persona input (<c>PUT /api/v1/personas/{id}</c>); same shape/validation as create.</summary>
/// <param name="NameAr">New Arabic label; required, ≤255 chars.</param>
/// <param name="NameEn">New English label; required, ≤255 chars.</param>
/// <param name="DescriptionAr">New optional Arabic description.</param>
/// <param name="DescriptionEn">New optional English description.</param>
public sealed record UpdatePersonaRequest(string NameAr, string NameEn, string? DescriptionAr, string? DescriptionEn);
