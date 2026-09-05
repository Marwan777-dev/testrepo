namespace Nabadat.KpiManagement.Application.Kpis.Dtos;

/// <summary>The outcome of an activation/deactivation command (see <see cref="KpiActivationResult"/>).</summary>
public enum KpiActivationOutcome
{
    /// <summary>The Active-state change (and any cascade) committed.</summary>
    Persisted,

    /// <summary>Deactivating a bound KPI needs explicit confirmation; the binding-usage counts are returned, nothing was written.</summary>
    RequiresConfirmation,

    /// <summary>No KPI exists for the supplied id.</summary>
    NotFound,
}
