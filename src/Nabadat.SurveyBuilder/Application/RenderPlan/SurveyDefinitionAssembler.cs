using Nabadat.SurveyBuilder.Application.Surveys.Interfaces;
using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.RenderPlan;

/// <summary>
/// Assembles the full <see cref="SurveyDefinition"/> M-04 needs to render an Active survey (T144),
/// backing <c>ISurveyRenderService.GetActiveSurveyDefinitionAsync</c>. Returns <c>null</c> when the
/// survey is not currently Active (status flips eligibility).
/// <para>Exposes only the fields whose shape is unambiguous today (status, locale, layout,
/// welcome/thanks HTML) — the richer authoring content (appearance tokens, per-question detail, the
/// inlined translation bundle) is <b>TODO-M01-008 (GAP)</b>, expanded when M-04's concrete rendering
/// needs are pinned; growing <see cref="SurveyDefinition"/> is an additive published-contract change.</para>
/// </summary>
public sealed class SurveyDefinitionAssembler
{
    private readonly ISurveyStore _surveys;

    public SurveyDefinitionAssembler(ISurveyStore surveys) => _surveys = surveys;

    public async Task<SurveyDefinition?> AssembleAsync(SurveyId surveyId, LocaleCode locale, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(surveyId.Value, ct);
        if (survey is null || survey.Status != SurveyStatus.Active)
        {
            return null;
        }

        return new SurveyDefinition(
            surveyId,
            survey.Status,
            locale,
            survey.Layout,
            survey.WelcomeHtml,
            survey.ThanksHtml);
    }
}
