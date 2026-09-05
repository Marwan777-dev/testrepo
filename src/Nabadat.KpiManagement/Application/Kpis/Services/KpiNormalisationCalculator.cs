using Nabadat.KpiManagement.Domain.ValueObjects;

namespace Nabadat.KpiManagement.Application.Kpis.Services;

/// <summary>
/// Maps a single raw KPI response onto the canonical 0–100 score (research.md / SRS §9). Pure
/// arithmetic with no state or I/O — hence <see langword="static"/>. Returns <see langword="decimal"/>
/// (not <see langword="double"/>) for exact arithmetic, matching the decimal money/score convention.
/// <para>
/// Linear scales map their <c>[min, max]</c> response range onto <c>[0, 100]</c>. Two special cases:
/// CES is inverted (high effort = low score, so effort 7 → 0 and effort 1 → 100), and the binary
/// FCR scale maps 0/1 onto 0/100. <see cref="Scale.Nps"/> is a passthrough — the −100..+100 NPS
/// score is already on its final scale.
/// </para>
/// </summary>
public static class KpiNormalisationCalculator
{
    private const decimal CesMin = 1m;
    private const decimal CesMax = 7m;

    /// <summary>
    /// Normalises a raw response on <paramref name="scale"/> to a 0–100 score by linearly mapping the
    /// scale's <c>[min, max]</c> range onto <c>[0, 100]</c>. <see cref="Scale.Nps"/> is returned
    /// unchanged (it is already a final −100..+100 score).
    /// </summary>
    public static decimal Normalise(Scale scale, decimal raw) => scale switch
    {
        Scale.Nps => raw,
        Scale.Scale0_10 => LinearMap(raw, 0m, 10m),
        Scale.Scale1_3 => LinearMap(raw, 1m, 3m),
        Scale.Scale1_5 => LinearMap(raw, 1m, 5m),
        Scale.Scale1_7 => LinearMap(raw, 1m, 7m),
        Scale.Scale1_10 => LinearMap(raw, 1m, 10m),
        Scale.Scale1_100 => LinearMap(raw, 1m, 100m),
        _ => throw new ArgumentOutOfRangeException(nameof(scale), scale, "Unsupported scale."),
    };

    /// <summary>
    /// Normalises a raw Customer Effort Score (1–7, inverted: lower effort is better). Effort 1 → 100,
    /// effort 7 → 0.
    /// </summary>
    public static decimal NormaliseCes(decimal raw) => (CesMax - raw) / (CesMax - CesMin) * 100m;

    /// <summary>
    /// Normalises a binary First-Contact-Resolution response (0 = not resolved, 1 = resolved) to
    /// 0 / 100.
    /// </summary>
    public static decimal NormaliseFcrBinary(decimal raw) => raw * 100m;

    private static decimal LinearMap(decimal raw, decimal min, decimal max) =>
        (raw - min) / (max - min) * 100m;
}
