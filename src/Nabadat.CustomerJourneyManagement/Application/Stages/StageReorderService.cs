using Nabadat.CustomerJourneyManagement.Application.Common;

namespace Nabadat.CustomerJourneyManagement.Application.Stages;

/// <summary>
/// The stage-reorder rule (T025 / US-1). A reorder request from
/// <c>PUT /api/v1/journeys/{id}/stages/reorder</c> supplies the complete new ordering as an array
/// of stage ids; per <c>contracts/journeys-api.md</c> it MUST be a <b>permutation</b> of the
/// journey's current stages — every existing stage present exactly once, no omissions, no
/// duplicates, no unknown ids. This is pure set logic with no I/O, so it lives in a stateless
/// helper that <see cref="StageService.ReorderStagesAsync"/> calls before it persists the new
/// order; keeping it separate makes the rule independently legible and reusable (e.g. by the API
/// layer for an early 422) without widening <c>StageService</c>'s constructor.
/// </summary>
public static class StageReorderService
{
    /// <summary>
    /// Validates that <paramref name="requestedOrder"/> is a permutation of
    /// <paramref name="existingStageIds"/>. Returns <c>null</c> when valid, or an
    /// <see cref="Error"/> with code <c>journey.invalid_stage_order</c> describing the first
    /// problem found (duplicate id, or set mismatch — a missing or unknown stage).
    /// </summary>
    public static Error? Validate(IReadOnlyList<Guid> existingStageIds, IReadOnlyList<Guid> requestedOrder)
    {
        ArgumentNullException.ThrowIfNull(existingStageIds);
        ArgumentNullException.ThrowIfNull(requestedOrder);

        var requested = new HashSet<Guid>(requestedOrder);
        if (requested.Count != requestedOrder.Count)
        {
            return new Error(
                "journey.invalid_stage_order",
                "The reorder request contains duplicate stage ids.");
        }

        if (!requested.SetEquals(existingStageIds))
        {
            return new Error(
                "journey.invalid_stage_order",
                "The reorder request must list exactly the journey's stages — no omissions, no unknown ids.");
        }

        return null;
    }
}
