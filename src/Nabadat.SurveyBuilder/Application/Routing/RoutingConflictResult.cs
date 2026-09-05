namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>Result of a <see cref="RoutingConflictDetector"/> run (T173).</summary>
/// <param name="Kind">Whether a cycle was detected, or none.</param>
public sealed record RoutingConflictResult(RoutingConflictKind Kind)
{
    /// <summary>Shared no-conflict result.</summary>
    public static readonly RoutingConflictResult None = new(RoutingConflictKind.None);

    /// <summary>Shared cycle-detected result.</summary>
    public static readonly RoutingConflictResult Cycle = new(RoutingConflictKind.CycleDetected);
}
