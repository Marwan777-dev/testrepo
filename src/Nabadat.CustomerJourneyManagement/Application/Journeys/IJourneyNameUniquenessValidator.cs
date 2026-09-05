using Nabadat.CustomerJourneyManagement.Application.Common;

namespace Nabadat.CustomerJourneyManagement.Application.Journeys;

/// <summary>
/// Validates that a journey name is free to use under the case-insensitive, Archived-excluding
/// uniqueness rule (<c>name</c> unique per tenant, case-insensitive, excluding Archived journeys —
/// <c>contracts/journeys-api.md</c>). Injected into <c>JourneyService</c> (create/rename) so the
/// pre-write check and the DB partial unique index (<c>idx_journeys_name_ci</c>) agree.
/// </summary>
public interface IJourneyNameUniquenessValidator
{
    /// <summary>
    /// Returns <see cref="ServiceResult.Success()"/> when <paramref name="name"/> is available, or a
    /// failure with code <c>journey.name_conflict</c> when a non-Archived journey already holds it
    /// (case-insensitive). <paramref name="excludeJourneyId"/> skips the journey being renamed so it
    /// never conflicts with itself.
    /// </summary>
    Task<ServiceResult> ValidateAsync(
        string name,
        Guid? excludeJourneyId = null,
        CancellationToken ct = default);
}
