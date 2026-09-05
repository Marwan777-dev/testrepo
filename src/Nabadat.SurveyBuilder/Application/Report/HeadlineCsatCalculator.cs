namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T233 [US8] — computes the headline CSAT gauge value (FR-13.2, spec.md § SC-007): the arithmetic
/// mean of the survey's contributing CSAT question values. Pure; unit-tested by
/// <c>HeadlineCsatCalculatorTests</c> (T227). Returns <c>null</c> when no question contributes so the
/// gauge renders empty rather than a misleading <c>0</c>.
/// </summary>
public sealed class HeadlineCsatCalculator
{
    /// <summary>
    /// The exact mean of <paramref name="questionValues"/> (no premature rounding — SC-007 pins a
    /// 0.01 tolerance), or <c>null</c> when the list is empty.
    /// </summary>
    public decimal? Compute(IReadOnlyList<decimal> questionValues)
    {
        if (questionValues.Count == 0)
        {
            return null;
        }

        decimal sum = 0m;
        foreach (var value in questionValues)
        {
            sum += value;
        }

        return sum / questionValues.Count;
    }
}
