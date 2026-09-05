using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.Application.Cxi;

/// <summary>
/// T086 [US-3] — assembles the <see cref="CxiSnapshotDto"/> for M-07 dashboard rendering from the
/// composite score plus its members' scores and weights. Pure logic, no state — hence
/// <see langword="static"/>. The composite score is carried through verbatim; each member's own score
/// is carried through and its effective percentage is derived from the member weights via
/// <see cref="CxiWeightNormaliser"/> (1 dp).
/// </summary>
public static class CxiSnapshotComposer
{
    /// <summary>Builds the snapshot: composite score + per-member breakdown (score + derived effective %).</summary>
    public static CxiSnapshotDto Compose(decimal compositeScore, IReadOnlyList<CxiMemberInput> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var percentages = CxiWeightNormaliser.Normalise(members.Select(m => m.Weight).ToList());

        var breakdown = members
            .Select((m, i) => new CxiMemberBreakdownDto(
                m.KpiId,
                m.KpiShortName,
                m.Score,
                EffectivePercentage: percentages.Count == members.Count ? percentages[i] : 0m))
            .ToList();

        return new CxiSnapshotDto(compositeScore, breakdown);
    }
}
