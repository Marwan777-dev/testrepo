namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The four report metric-card values (FR-13.4): total <see cref="Responses"/>, the
/// <see cref="CompletionRate"/> in <c>[0,1]</c>, the <see cref="MedianTimeSeconds"/> (always
/// available), and the distinct <see cref="Touchpoints"/> count. Application-layer result mapped to
/// the wire <c>MetricCards</c> by the Api layer.
/// </summary>
public sealed record ReportMetrics(
    int Responses,
    decimal CompletionRate,
    int? MedianTimeSeconds,
    int Touchpoints);
