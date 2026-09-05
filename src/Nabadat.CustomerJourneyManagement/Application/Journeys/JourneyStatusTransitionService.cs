using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Application.Events;
using Nabadat.CustomerJourneyManagement.Application.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;
using Nabadat.CustomerJourneyManagement.Domain.ValueObjects;

namespace Nabadat.CustomerJourneyManagement.Application.Journeys;

/// <summary>
/// The journey lifecycle state machine (T022 / US-1). Enforces the transitions defined in
/// <c>contracts/journeys-api.md</c> (<c>PATCH /api/v1/journeys/{id}/status</c>):
/// <list type="bullet">
///   <item><description><c>Draft → Active</c></description></item>
///   <item><description><c>Active → Inactive</c></description></item>
///   <item><description><c>Inactive → Active</c></description></item>
///   <item><description><c>Draft | Active | Inactive → Archived</c></description></item>
///   <item><description><c>Archived → any</c> is rejected — <c>Archived</c> is terminal</description></item>
/// </list>
/// Every accepted transition persists the new status and publishes a
/// <c>journey.status.changed</c> M-17 event in the <b>same</b> unit of work (FR-015), via
/// <see cref="ITenantDbContext.ExecuteAsync(Func{Task}, CancellationToken)"/>, so the row update
/// and the audit row commit atomically.
/// Validation runs <i>before</i> the transaction opens, so a rejected transition writes nothing.
/// </summary>
public sealed class JourneyStatusTransitionService
{
    private readonly IJourneyDataService _journeys;
    private readonly ITenantDbContext _db;
    private readonly IM17EventPublisher _events;
    private readonly TimeProvider _time;

    public JourneyStatusTransitionService(
        IJourneyDataService journeys,
        ITenantDbContext db,
        IM17EventPublisher events,
        TimeProvider time)
    {
        _journeys = journeys;
        _db = db;
        _events = events;
        _time = time;
    }

    /// <summary>
    /// Transitions <paramref name="journeyId"/> to <paramref name="target"/> on behalf of
    /// <paramref name="actor"/>. Returns <see cref="ServiceResult.Success()"/> on an accepted
    /// transition (status persisted + <c>journey.status.changed</c> published in one tx), or a
    /// failure carrying <c>journey.not_found</c>, <c>journey.archived_terminal</c>, or
    /// <c>journey.invalid_transition</c>. No write occurs on any failure path.
    /// </summary>
    public async Task<ServiceResult> ChangeStatusAsync(
        Guid journeyId,
        JourneyStatus target,
        ActorContext actor,
        CancellationToken ct = default)
    {
        var journey = await _journeys.GetByIdAsync(journeyId, ct);
        if (journey is null)
        {
            return ServiceResult.Failure("journey.not_found", $"Journey {journeyId} does not exist.");
        }

        // Stored status is the exact PascalCase member name (JourneyStatus value-object contract).
        if (!Enum.TryParse<JourneyStatus>(journey.Status, ignoreCase: false, out var current))
        {
            return ServiceResult.Failure(
                "journey.invalid_transition",
                $"Journey status '{journey.Status}' is not a recognized lifecycle state.");
        }

        // Archived is terminal: any outbound transition is rejected with its own code so the
        // caller can distinguish "you cannot leave Archived" from a merely undefined step.
        if (current == JourneyStatus.Archived)
        {
            return ServiceResult.Failure(
                "journey.archived_terminal",
                "Archived is a terminal status; the journey cannot transition to another status.");
        }

        if (!IsValidTransition(current, target))
        {
            return ServiceResult.Failure(
                "journey.invalid_transition",
                $"Transition {current} → {target} is not an allowed journey lifecycle step.");
        }

        // NOTE: the contract's `journey.archive_blocked_active_surveys` (409) guard is intentionally
        // not enforced here — M-16 holds no survey-binding source in US-1; that cross-module check
        // lands with survey integration. The state machine above is complete for US-1.

        var occurredAt = _time.GetUtcNow();

        await _db.ExecuteAsync(async () =>
        {
            journey.Status = target.ToString();
            journey.UpdatedBy = actor.UserId;
            journey.UpdatedAt = occurredAt;
            await _journeys.UpdateAsync(journey, ct);

            await _events.PublishAsync(
                CustomerJourneyManagementEvent.JourneyStatusChanged(
                    actor.UserId,
                    actor.Persona,
                    journeyId,
                    occurredAt,
                    actor.CorrelationId,
                    newValue: new { status = target.ToString() },
                    oldValue: new { status = current.ToString() }),
                ct);
        }, ct);

        return ServiceResult.Success();
    }

    /// <summary>The allowed (from → to) lifecycle steps; everything else is rejected.</summary>
    private static bool IsValidTransition(JourneyStatus from, JourneyStatus to) => (from, to) switch
    {
        (JourneyStatus.Draft, JourneyStatus.Active) => true,
        (JourneyStatus.Active, JourneyStatus.Inactive) => true,
        (JourneyStatus.Inactive, JourneyStatus.Active) => true,
        (JourneyStatus.Draft, JourneyStatus.Archived) => true,
        (JourneyStatus.Active, JourneyStatus.Archived) => true,
        (JourneyStatus.Inactive, JourneyStatus.Archived) => true,
        _ => false,
    };
}
