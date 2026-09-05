namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Thin M-01 published diagnostic (constitution AD-01) consumed by the <b>M-02</b> admin surface to
/// answer "is this survey deliverable?" without instantiating a full render plan. Kept deliberately
/// small so M-02's admin rule builder can quickly filter the survey dropdown. See
/// <c>contracts/published-interface.md</c>. Uses <see cref="SurveyId"/> (declared alongside
/// <see cref="ISurveyRenderService"/>).
/// </summary>
public interface IActiveSurveyDefinitionProbe
{
    /// <summary>
    /// <c>true</c> when the survey is <c>Active</c> AND passes the Publish content-gate (BR-1.7:
    /// ≥ 1 section AND ≥ 1 question).
    /// </summary>
    Task<bool> IsDeliverableAsync(SurveyId surveyId, CancellationToken ct);
}
