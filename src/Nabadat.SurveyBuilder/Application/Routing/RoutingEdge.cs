namespace Nabadat.SurveyBuilder.Application.Routing;

/// <summary>
/// A single resolved routing override, positioned by survey order for cycle detection (T173). The
/// source question sits at <paramref name="SourceOrder"/>; answering <paramref name="AnswerKey"/>
/// jumps to the question at <paramref name="TargetOrder"/>, or to end-of-survey when that is null.
/// </summary>
/// <param name="SourceOrder">Zero-based position of the source question in survey order.</param>
/// <param name="AnswerKey">Per-type answer identifier this edge branches on.</param>
/// <param name="TargetOrder">Position of the target question; null ⇒ end of survey.</param>
public readonly record struct RoutingEdge(int SourceOrder, string AnswerKey, int? TargetOrder);
