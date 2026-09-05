namespace Nabadat.KpiManagement.Application.Kpis.Validators;

/// <summary>
/// Advisory (non-blocking) rule for a KPI's Target relative to its performance bands. A target that
/// falls below the Satisfactory band — i.e. under the average/satisfactory boundary <c>y</c> (the
/// frontend flags <c>target &lt; y</c>) — is permitted but flagged, because teams normally aim for the
/// Satisfactory range. Pure logic, no state (hence <see langword="static"/>), mirroring
/// <see cref="TopNBoxWarningRule"/>; the frontend keeps a TS twin (KpiConfigForm's
/// <c>targetBelowSatisfactory</c>) for the live form warning. Never blocks a save.
/// </summary>
public static class TargetBandRule
{
    /// <summary>
    /// True when <paramref name="target"/> sits below the Satisfactory band (&lt; <paramref name="y"/>,
    /// the average/satisfactory boundary). Non-blocking — surfaces as an advisory, never an error.
    /// </summary>
    public static bool IsBelowSatisfactory(decimal target, decimal y) => target < y;
}
