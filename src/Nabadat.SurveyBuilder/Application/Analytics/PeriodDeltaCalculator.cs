namespace Nabadat.SurveyBuilder.Application.Analytics;

/// <summary>
/// T256 [US9] — computes a headline number's deviation vs the previous period of equal length
/// (FR-14.3, FR-14.5). A <see cref="DeltaKind.Rate"/> deviates in percentage points
/// (<c>current − prior</c>); a <see cref="DeltaKind.Count"/> deviates in percent change
/// (<c>(current − prior) / prior × 100</c>, rounded to 2 dp away from zero). Pure — no I/O, no clock.
/// Unit-tested by <c>PeriodDeltaCalculatorTests</c> (T251).
/// <para><b>Suppression (FR-14.5):</b> a <c>null</c> prior (new survey — no previous period) returns
/// <c>null</c>; a <see cref="DeltaKind.Count"/> delta against a zero prior also returns <c>null</c>
/// (a percent change from a zero base is undefined and must be suppressed, not shown as a misleading
/// number). A <see cref="DeltaKind.Rate"/> delta is a plain subtraction, so a zero prior is fine.</para>
/// </summary>
public sealed class PeriodDeltaCalculator
{
    public decimal? Delta(decimal current, decimal? prior, DeltaKind kind)
    {
        if (prior is not { } priorValue)
        {
            return null;
        }

        return kind switch
        {
            DeltaKind.Rate => current - priorValue,
            DeltaKind.Count => priorValue == 0m
                ? null
                : Math.Round((current - priorValue) / priorValue * 100m, 2, MidpointRounding.AwayFromZero),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown delta kind."),
        };
    }
}
