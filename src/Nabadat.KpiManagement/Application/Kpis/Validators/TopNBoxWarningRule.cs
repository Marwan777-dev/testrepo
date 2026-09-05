using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Kpis.Validators;

/// <summary>
/// The advisory + blocking rules for the TOP-n Box calculation method's <c>n</c> value
/// (FR-014 / FR-015). Pure logic, no state — hence <see langword="static"/>.
/// <para>
/// <see cref="ShouldWarn"/> raises a non-blocking advisory when <c>n</c> exceeds half the scale's
/// span (a "top half or more" box selection rarely discriminates between performers).
/// <see cref="IsBlockingError"/> is the hard stop: <c>n</c> reaching the scale's box count would put
/// every response in the top-n box, making the metric meaningless.
/// </para>
/// </summary>
public static class TopNBoxWarningRule
{
    /// <summary>True when <paramref name="n"/> exceeds half of <paramref name="scale"/>'s span (advisory only).</summary>
    public static bool ShouldWarn(Scale scale, int n) => n > Span(scale) / 2.0;

    /// <summary>True when <paramref name="n"/> reaches <paramref name="scale"/>'s box count (blocking error).</summary>
    public static bool IsBlockingError(Scale scale, int n) => n >= BoxCount(scale);

    /// <summary>The number of discrete response boxes on the scale (e.g. Scale1_7 → 7, Scale0_10 → 11).</summary>
    private static int BoxCount(Scale scale)
    {
        var (min, max) = Range(scale);
        return max - min + 1;
    }

    /// <summary>The scale span (max − min; e.g. Scale1_7 → 6, Scale0_10 → 10).</summary>
    private static int Span(Scale scale)
    {
        var (min, max) = Range(scale);
        return max - min;
    }

    private static (int Min, int Max) Range(Scale scale) => scale switch
    {
        Scale.Scale0_10 => (0, 10),
        Scale.Scale1_3 => (1, 3),
        Scale.Scale1_5 => (1, 5),
        Scale.Scale1_7 => (1, 7),
        Scale.Scale1_10 => (1, 10),
        Scale.Scale1_100 => (1, 100),
        // NPS uses the NPSStandard method, not TOP-n Box; treat its range as the raw 0..10 likelihood
        // scale so the rule never throws if mis-invoked.
        Scale.Nps => (0, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unsupported scale."),
    };
}
