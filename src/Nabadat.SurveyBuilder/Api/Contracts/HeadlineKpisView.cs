using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// The report's headline KPI gauges (contracts/report-and-analytics.md — <c>headline_kpis</c>). Each
/// is <c>null</c> when the survey has no contributing question of that family (e.g. no NPS question ⇒
/// <see cref="Nps"/> is omitted rather than shown as zero).
/// </summary>
public sealed record HeadlineKpisView(
    [property: JsonPropertyName("csat")] HeadlineKpi? Csat,
    [property: JsonPropertyName("nps")] HeadlineKpi? Nps,
    [property: JsonPropertyName("ces")] HeadlineKpi? Ces);
