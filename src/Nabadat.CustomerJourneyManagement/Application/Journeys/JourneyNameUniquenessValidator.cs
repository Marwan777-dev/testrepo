using Nabadat.CustomerJourneyManagement.Application.Common;
using Nabadat.CustomerJourneyManagement.Domain.Interfaces;

namespace Nabadat.CustomerJourneyManagement.Application.Journeys;

/// <summary>
/// Default <see cref="IJourneyNameUniquenessValidator"/>. Delegates the case-insensitive,
/// Archived-excluding lookup to <see cref="IJourneyDataService.ExistsActiveByNameAsync"/> (which
/// backs the functional partial unique index <c>idx_journeys_name_ci</c> —
/// <c>LOWER(name) WHERE status &lt;&gt; 'Archived'</c>) and maps a live hit to the
/// <c>journey.name_conflict</c> error. An Archived journey is excluded by the repository query, so
/// its name is reported as free to reuse.
/// </summary>
public sealed class JourneyNameUniquenessValidator : IJourneyNameUniquenessValidator
{
    private readonly IJourneyDataService _journeys;

    public JourneyNameUniquenessValidator(IJourneyDataService journeys) => _journeys = journeys;

    /// <inheritdoc />
    public async Task<ServiceResult> ValidateAsync(
        string name,
        Guid? excludeJourneyId = null,
        CancellationToken ct = default)
    {
        var nameTaken = await _journeys.ExistsActiveByNameAsync(name, excludeJourneyId, ct);

        return nameTaken
            ? ServiceResult.Failure("journey.name_conflict", "A journey with this name already exists.")
            : ServiceResult.Success();
    }
}
