namespace Nabadat.Platform.Contracts.M16;

/// <summary>
/// Published interface: M-16 (outward-facing).
/// Consumers call this to retrieve the latest computed scores for a journey.
/// M-16 delegates computation to M-06, persists the result, and publishes an event.
/// </summary>
public interface IJourneyScoreProvider
{
    /// <summary>
    /// Computes (or refreshes) scores for the given journey.
    /// Always triggers a fresh computation via M-06.
    /// Returns null if the journey has no measured touchpoints.
    /// </summary>
    Task<JourneyScoreResultDto?> GetScoresAsync(Guid journeyId, CancellationToken ct = default);
}

public record JourneyScoreResultDto(
    Guid JourneyId,
    decimal? JourneyScore,
    DateTime ComputedAt,
    IReadOnlyList<StageScoreDto> StageScores,
    IReadOnlyList<TouchpointScoreDto> TouchpointScores
);

public record StageScoreDto(
    Guid StageId,
    decimal? Score,
    int MeasuredTouchpointCount
);

public record TouchpointScoreDto(
    Guid TouchpointId,
    decimal? Score,
    IReadOnlyList<KpiScoreDto> KpiScores
);

public record KpiScoreDto(
    string KpiType,
    decimal? Score,
    int ResponseCount
);
