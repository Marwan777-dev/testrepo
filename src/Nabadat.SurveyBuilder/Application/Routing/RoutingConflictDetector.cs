namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// T173 [US4] — detects loops in a set of routing overrides. Routes are forward-only by design
/// (research.md §6): a route whose target sits earlier in survey order than its source
/// (<c>TargetOrder &lt; SourceOrder</c>) is a back-edge that could re-enter an already-answered
/// question, so the App layer rejects the save. Forward routes and end-of-survey routes
/// (<c>TargetOrder == null</c>) are conflict-free. Pure — no I/O.
/// </summary>
public sealed class RoutingConflictDetector
{
    /// <summary>Returns <see cref="RoutingConflictKind.CycleDetected"/> if any route points backward.</summary>
    public RoutingConflictResult Detect(IReadOnlyList<RoutingEdge> routes)
    {
        foreach (var edge in routes)
        {
            if (edge.TargetOrder is { } target && target < edge.SourceOrder)
            {
                return RoutingConflictResult.Cycle;
            }
        }

        return RoutingConflictResult.None;
    }
}
