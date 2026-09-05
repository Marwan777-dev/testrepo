using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Routing;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Routing;

/// <summary>
/// T165 [US4] — unit tests for <c>RoutingConflictDetector</c>. A route that points back to a
/// question earlier in the survey order creates a loop; the detector reports it so the App layer
/// can reject the save. Forward-only routes (and routes to <c>__end</c>) are conflict-free.
/// <para>
/// Contract pinned for the implementer (T173):
/// <list type="bullet">
///   <item><c>RoutingConflictDetector</c> lives in <c>Application/Routing/</c> and is pure:
///   <c>RoutingConflictResult Detect(IReadOnlyList&lt;RoutingEdge&gt; routes)</c>.</item>
///   <item><c>RoutingEdge</c> (in <c>Application/Routing/</c>) carries the source question's
///   position <c>int SourceOrder</c>, the <c>string AnswerKey</c>, and the target's position
///   <c>int? TargetOrder</c> (null ⇒ end-of-survey).</item>
///   <item><c>RoutingConflictResult</c> exposes <c>RoutingConflictKind Kind</c> with members
///   <c>None</c> and <c>CycleDetected</c>; a back-edge (<c>TargetOrder &lt; SourceOrder</c>) yields
///   <c>CycleDetected</c>.</item>
/// </list>
/// </para>
/// </summary>
public sealed class RoutingConflictDetectorTests
{
    private readonly RoutingConflictDetector _detector = new();

    [Fact]
    public void Detect_reports_a_cycle_when_a_route_points_back_to_a_prior_question()
    {
        // Question at order 2 routes back to the question at order 0.
        var routes = new[]
        {
            new RoutingEdge(SourceOrder: 2, AnswerKey: "yes", TargetOrder: 0),
        };

        _detector.Detect(routes).Kind.Should().Be(RoutingConflictKind.CycleDetected);
    }

    [Fact]
    public void Detect_reports_no_conflict_when_every_route_points_forward()
    {
        var routes = new[]
        {
            new RoutingEdge(SourceOrder: 0, AnswerKey: "yes", TargetOrder: 3),
            new RoutingEdge(SourceOrder: 1, AnswerKey: "no", TargetOrder: 2),
        };

        _detector.Detect(routes).Kind.Should().Be(RoutingConflictKind.None);
    }

    [Fact]
    public void Detect_reports_no_conflict_when_a_route_ends_the_survey()
    {
        var routes = new[]
        {
            new RoutingEdge(SourceOrder: 1, AnswerKey: "1", TargetOrder: null),
        };

        _detector.Detect(routes).Kind.Should().Be(RoutingConflictKind.None);
    }
}
