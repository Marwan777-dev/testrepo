using Nabadat.SurveyBuilder.Domain.Interfaces;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Questions;

/// <summary>
/// Adjusts a KPI question's binding when its KPI code changes (T077, BR-8.5): the touchpoint is
/// retained iff still valid for the new KPI + journey + stage (per the M-16 <see cref="IJourneyReader"/>),
/// else cleared; a stage that is no longer valid is cleared too.
/// </summary>
public sealed class KpiBindingChangePolicy
{
    private readonly IJourneyReader _journeys;

    public KpiBindingChangePolicy(IJourneyReader journeys) => _journeys = journeys;

    public async Task<KpiBinding> OnKpiChangedAsync(KpiBinding current, string newKpiCode, CancellationToken ct = default)
    {
        var result = current with { KpiCode = newKpiCode };

        if (!current.BoundJourneyOn)
        {
            return result;
        }

        // Full binding (stage + touchpoint) still valid for the new KPI → retain both.
        if (await _journeys.IsBindingValidAsync(newKpiCode, current.StageId, current.TouchpointId, ct))
        {
            return result;
        }

        // Touchpoint no longer valid, but the stage alone is → drop only the touchpoint.
        if (current.TouchpointId is not null
            && await _journeys.IsBindingValidAsync(newKpiCode, current.StageId, null, ct))
        {
            return result with { TouchpointId = null };
        }

        // Neither valid → clear both.
        return result with { StageId = null, TouchpointId = null };
    }
}
