namespace Nabadat.SurveyBuilder.Application.Surveys;

/// <summary>
/// The outcome of an <see cref="ApprovalStateMachine.Publish"/> authorization decision: whether the
/// actor may publish (and the survey moves to Active) or is refused (BR-15.2, FR-15.5).
/// </summary>
public enum PublishDecision
{
    /// <summary>The actor may publish; the survey transitions to <c>Active</c>.</summary>
    Published,

    /// <summary>The actor may not publish; the survey status is left unchanged.</summary>
    Forbidden,
}
