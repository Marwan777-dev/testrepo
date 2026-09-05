using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.RenderPlan;

/// <summary>
/// Implements the M-01 published <see cref="IActiveSurveyReader"/> (T146) — M-04 calls it at
/// response-submission time to enforce the active-period lifecycle (BR-3.4). Reads the survey's
/// lifecycle status, activation instant, and absolute expiry from <see cref="ISurveyStore"/>.
/// <para>The absolute <c>ExpiresAt</c> is derived as
/// <see cref="Survey.ActivatedAt"/> + <see cref="Survey.ActivePeriod"/>. It is <c>null</c> — meaning
/// "never auto-expires" (FR-3.4) — whenever the survey has no active period, or has not yet been
/// activated (no start instant to measure from).</para>
/// </summary>
public sealed class ActiveSurveyReader : IActiveSurveyReader
{
    private readonly ISurveyStore _surveys;

    public ActiveSurveyReader(ISurveyStore surveys) => _surveys = surveys;

    public async Task<ActiveSurveyState> GetStateAsync(SurveyId surveyId, DateTimeOffset asOf, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(surveyId.Value, ct);

        // A missing survey is not collectable — report a terminal status M-04 will reject.
        if (survey is null)
        {
            return new ActiveSurveyState(SurveyStatus.Archived, ActivatedAt: null, ExpiresAt: null);
        }

        // Expiry is the start instant plus the active window; null period ⇒ never auto-expires (FR-3.4).
        var expiresAt = survey is { ActivatedAt: { } activatedAt, ActivePeriod: { } period }
            ? activatedAt + period.ToTimeSpan()
            : (DateTimeOffset?)null;

        return new ActiveSurveyState(survey.Status, survey.ActivatedAt, expiresAt);
    }
}
