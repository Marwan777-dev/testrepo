namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>
/// Outcome of <c>KpiActivationCommandHandler.HandleAsync</c>. On
/// <see cref="KpiActivationOutcome.RequiresConfirmation"/> the <see cref="TouchpointCount"/> /
/// <see cref="JourneyCount"/> carry the M-16 binding usage the API surfaces in the 409 body; they are
/// zero for the other outcomes.
/// </summary>
public sealed record KpiActivationResult(
    KpiActivationOutcome Outcome,
    int TouchpointCount,
    int JourneyCount)
{
    /// <summary>The Active-state change (and any CXI cascade) committed.</summary>
    public static KpiActivationResult Persisted() => new(KpiActivationOutcome.Persisted, 0, 0);

    /// <summary>Deactivation of a bound KPI needs confirmation; nothing was written.</summary>
    public static KpiActivationResult RequiresConfirmation(int touchpoints, int journeys) =>
        new(KpiActivationOutcome.RequiresConfirmation, touchpoints, journeys);

    /// <summary>No KPI exists for the supplied id.</summary>
    public static KpiActivationResult NotFound() => new(KpiActivationOutcome.NotFound, 0, 0);
}
