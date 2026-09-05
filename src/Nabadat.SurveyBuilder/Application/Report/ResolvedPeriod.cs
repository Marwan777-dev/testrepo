namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// A concrete report window resolved from a named period (see <see cref="PeriodResolver"/>). Both
/// bounds are absolute instants; <see cref="To"/> is the anchor "now" and <see cref="From"/> is the
/// window's start. Serialised on the wire as <c>{ resolved_from, resolved_to }</c>
/// (contracts/report-and-analytics.md).
/// </summary>
public sealed record ResolvedPeriod(DateTimeOffset From, DateTimeOffset To);
