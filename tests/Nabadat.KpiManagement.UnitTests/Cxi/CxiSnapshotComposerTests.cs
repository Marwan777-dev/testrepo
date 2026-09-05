using FluentAssertions;
using Xunit;
using Nabadat.KpiManagement.Application.Cxi;
using Nabadat.KpiManagement.Application.Kpis.Dtos;

namespace Nabadat.KpiManagement.UnitTests.Cxi;

/// <summary>
/// T081 [US3] — unit tests for <c>CxiSnapshotComposer</c> (builds the M-07 dashboard snapshot from the
/// composite score plus member scores + weights), covering the spec.md US-3 Required cases.
/// <para>
/// Contract pinned for the implementer (T086):
/// <list type="bullet">
///   <item>Static <c>CxiSnapshotComposer</c> in <c>Application/Cxi/</c>.</item>
///   <item>A member input record <c>CxiMemberInput(Guid KpiId, string KpiShortName, int Weight, decimal Score)</c>
///   in <c>Application/Cxi/</c>.</item>
///   <item><c>Compose(decimal compositeScore, IReadOnlyList&lt;CxiMemberInput&gt; members)</c> →
///   <see cref="CxiSnapshotDto"/>: <c>CompositeScore</c> carried through verbatim; each member becomes a
///   <see cref="CxiMemberBreakdownDto"/> with its own <c>Score</c> carried through and its
///   <c>EffectivePercentage</c> derived from the weights via <c>CxiWeightNormaliser</c> (1 dp).</item>
/// </list>
/// </para>
/// </summary>
public sealed class CxiSnapshotComposerTests
{
    private static readonly Guid Nps = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Csat = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    private static readonly Guid Ces = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    private static IReadOnlyList<CxiMemberInput> Members() => new[]
    {
        new CxiMemberInput(Nps, "NPS", Weight: 3, Score: 60m),
        new CxiMemberInput(Csat, "CSAT", Weight: 2, Score: 90m),
        new CxiMemberInput(Ces, "CES", Weight: 1, Score: 80m),
    };

    [Fact]
    public void Compose_carries_the_composite_score_through()
    {
        CxiSnapshotComposer.Compose(78.4m, Members())
            .CompositeScore.Should().Be(78.4m);
    }

    [Fact]
    public void Compose_derives_effective_percentages_from_member_weights()
    {
        CxiSnapshotComposer.Compose(78.4m, Members())
            .MemberBreakdown.Select(m => m.EffectivePercentage)
            .Should().Equal(50.0m, 33.3m, 16.7m);
    }

    [Fact]
    public void Compose_carries_member_scores_through()
    {
        CxiSnapshotComposer.Compose(78.4m, Members())
            .MemberBreakdown.Select(m => m.Score)
            .Should().Equal(60m, 90m, 80m);
    }
}
