using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// A headline KPI gauge on the wire (FR-13.1/13.2, contracts/report-and-analytics.md): the current
/// <c>value</c>, its <c>target</c> marker (null when the tenant catalogue has not set one), and the
/// period-over-period delta in percentage points. <see cref="DeltaPp"/> is <c>null</c> when there is
/// no previous-period data (FR-14.5 — no misleading <c>+0</c>).
/// </summary>
public sealed record HeadlineKpi(
    [property: JsonPropertyName("value")] decimal Value,
    [property: JsonPropertyName("target")] decimal? Target,
    [property: JsonPropertyName("delta_pp")] decimal? DeltaPp)
{
    /// <summary>Maps an Application-layer <see cref="ReportHeadlineKpi"/> (null ⇒ null gauge).</summary>
    public static HeadlineKpi? From(ReportHeadlineKpi? kpi) =>
        kpi is null ? null : new HeadlineKpi(kpi.Value, kpi.Target, kpi.DeltaPp);
}
