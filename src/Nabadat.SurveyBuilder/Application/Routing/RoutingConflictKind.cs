namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>Outcome kind of a <see cref="RoutingConflictDetector"/> run (T173).</summary>
public enum RoutingConflictKind
{
    /// <summary>Every route points forward (or ends the survey) — safe to save.</summary>
    None,

    /// <summary>At least one route points back to an earlier question, forming a loop.</summary>
    CycleDetected,
}
