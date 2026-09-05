using System.Diagnostics.CodeAnalysis;

namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T234 [US8] — turns a named report period (FR-13.1) into a concrete <see cref="ResolvedPeriod"/>
/// anchored at the caller-supplied <c>now</c>: <see cref="ResolvedPeriod.To"/> is always <c>now</c>
/// and <see cref="ResolvedPeriod.From"/> is <c>now</c> minus the named window. Pure — the clock is
/// injected, never read (CLAUDE.md Unit Test Policy rule 8); unit-tested by
/// <c>PeriodResolverTests</c> (T228).
/// <para><c>custom</c> is deliberately NOT resolved here — it needs explicit <c>from</c>/<c>to</c>
/// bounds supplied at the controller (contracts/report-and-analytics.md, <c>400
/// report.period.invalid</c> when they are missing). Any unrecognised period throws.</para>
/// </summary>
public sealed class PeriodResolver
{
    /// <summary>
    /// Resolves one of <c>last_1_day</c> / <c>last_7_days</c> / <c>last_month</c> /
    /// <c>last_3_months</c> / <c>last_6_months</c> / <c>last_9_months</c> / <c>last_year</c> against
    /// <paramref name="now"/>. Day windows subtract calendar days; month/year windows subtract
    /// calendar months/years.
    /// </summary>
    /// <exception cref="ArgumentException">The period is not a known named window.</exception>
    public ResolvedPeriod Resolve([DisallowNull] string period, DateTimeOffset now)
    {
        var from = period switch
        {
            "last_1_day" => now.AddDays(-1),
            "last_7_days" => now.AddDays(-7),
            "last_month" => now.AddMonths(-1),
            "last_3_months" => now.AddMonths(-3),
            "last_6_months" => now.AddMonths(-6),
            "last_9_months" => now.AddMonths(-9),
            "last_year" => now.AddYears(-1),
            _ => throw new ArgumentException($"Unknown report period '{period}'.", nameof(period)),
        };

        return new ResolvedPeriod(from, now);
    }
}
