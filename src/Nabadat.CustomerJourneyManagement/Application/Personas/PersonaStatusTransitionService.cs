using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Personas;

/// <summary>
/// The persona lifecycle state machine (T063 / US-3). Enforces the transitions defined in
/// <c>contracts/personas-api.md</c> (<c>PATCH /api/v1/personas/{id}/status</c>):
/// <list type="bullet">
///   <item><description><c>Draft → Active</c></description></item>
///   <item><description><c>Active → Inactive</c></description></item>
///   <item><description><c>Inactive → Active</c></description></item>
///   <item><description><c>Draft | Active | Inactive → Archived</c></description></item>
///   <item><description><c>Archived → any</c> is rejected — <c>Archived</c> is terminal</description></item>
/// </list>
/// Archiving carries an extra guard the journey state machine does not: a persona bound to one
/// or more journeys cannot be archived (<c>persona.archive_blocked_active_bindings</c>, 409) — the
/// caller must unbind first. Every accepted transition persists the new status and publishes a
/// <c>persona.status.changed</c> M-17 event in the <b>same</b> unit of work (FR-015), via
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>, so the row update
/// and the audit row commit atomically.
/// All validation (and the binding-count guard) runs <i>before</i> the transaction opens, so a
/// rejected transition writes nothing.
/// </summary>
public sealed class PersonaStatusTransitionService
{
    private readonly IPersonaDataService _personas;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public PersonaStatusTransitionService(
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
    /// Transitions <paramref name="personaId"/> to <paramref name="target"/> on behalf of
    /// <paramref name="actor"/>. Returns <see cref="ServiceResult.Success()"/> on an accepted
    /// transition (status persisted + <c>persona.status.changed</c> published in one tx), or a
    /// failure carrying <c>persona.not_found</c>, <c>persona.archived_terminal</c>,
    /// <c>persona.invalid_transition</c>, or <c>persona.archive_blocked_active_bindings</c>.
    /// No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult> ChangeStatusAsync(
        Guid personaId,
        PersonaStatus target,
        ActorContext actor,
        CancellationToken ct = default)
    {
        var persona = await _personas.GetByIdAsync(personaId, ct);
        if (persona is null)
        {
            return ServiceResult.Failure("persona.not_found", $"Persona {personaId} does not exist.");
        }

        // Stored status is the exact PascalCase member name (PersonaStatus value-object contract).
        if (!Enum.TryParse<PersonaStatus>(persona.Status, ignoreCase: false, out var current))
        {
            return ServiceResult.Failure(
                "persona.invalid_transition",
                $"Persona status '{persona.Status}' is not a recognized lifecycle state.");
        }

        // Archived is terminal: any outbound transition is rejected with its own code so the
        // caller can distinguish "you cannot leave Archived" from a merely undefined step.
        if (current == PersonaStatus.Archived)
        {
            return ServiceResult.Failure(
                "persona.archived_terminal",
                "Archived is a terminal status; the persona cannot transition to another status.");
        }

        if (!IsValidTransition(current, target))
        {
            return ServiceResult.Failure(
                "persona.invalid_transition",
                $"Transition {current} → {target} is not an allowed persona lifecycle step.");
        }

        // Archive guard: a persona bound to one or more journeys cannot be archived (FR-005).
        // Checked before the transaction opens, so a blocked archive writes nothing.
        if (target == PersonaStatus.Archived)
        {
            var bindingCount = await _personas.CountBindingsAsync(personaId, ct);
            if (bindingCount > 0)
            {
                return ServiceResult.Failure(
                    "persona.archive_blocked_active_bindings",
                    $"Persona {personaId} is bound to {bindingCount} journey(s); unbind before archiving.");
            }
        }

        var occurredAt = _time.GetUtcNow();

        await _db.ExecuteAsync(async () =>
        {
            persona.Status = target.ToString();
            persona.UpdatedBy = actor.UserId;
            persona.UpdatedAt = occurredAt;
            await _personas.UpdateAsync(persona, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.PersonaStatusChanged(
                    actor.UserId,
                    actor.Persona,
                    personaId,
                    occurredAt,
                    actor.CorrelationId,
                    newValue: new { status = target.ToString() },
                    oldValue: new { status = current.ToString() }),
                ct);
        }, ct);

        return ServiceResult.Success();
    }

    /// <summary>The allowed (from → to) lifecycle steps; everything else is rejected.</summary>
    private static bool IsValidTransition(PersonaStatus from, PersonaStatus to) => (from, to) switch
    {
        (PersonaStatus.Draft, PersonaStatus.Active) => true,
        (PersonaStatus.Active, PersonaStatus.Inactive) => true,
        (PersonaStatus.Inactive, PersonaStatus.Active) => true,
        (PersonaStatus.Draft, PersonaStatus.Archived) => true,
        (PersonaStatus.Active, PersonaStatus.Archived) => true,
        (PersonaStatus.Inactive, PersonaStatus.Archived) => true,
        _ => false,
    };
}
