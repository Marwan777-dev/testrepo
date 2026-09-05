using FluentAssertions;
using Nabadat.KpiManagement.Domain.ValueObjects;
using Xunit;
using Nabadat.KpiManagement.Application.Kpis.Services;

namespace Nabadat.KpiManagement.UnitTests.Kpis;

/// <summary>
/// T048 [US2] — unit tests for <c>KpiNormalisationCalculator</c> (raw response → 0–100 score),
/// covering the spec.md US-2 Required cases (linear scales, the inverted CES scale, the binary FCR
/// scale, and the NPS raw passthrough).
/// <para>
/// Contract pinned for the implementer (T052):
/// <list type="bullet">
///   <item>Static <c>KpiNormalisationCalculator</c> in <c>Application/Kpis/</c> returning
///   <see langword="decimal"/> (NOT <c>double</c> — exact arithmetic, matching the decimal money/score
///   convention in research.md).</item>
///   <item><c>Normalise(Scale scale, decimal raw)</c> — linear map onto 0–100 for the standard scales,
///   and a raw passthrough for <c>Scale.Nps</c> (the −100..+100 NPS score is already on its final
///   scale).</item>
///   <item><c>NormaliseCes(decimal raw)</c> — inverted 1–7 (high effort = low score) and
///   <c>NormaliseFcrBinary(decimal raw)</c> — binary 0/1 → 0/100.</item>
/// </list>
/// </para>
/// <para><b>Required domain follow-up (T052):</b> the spec's <c>Normalise(Scale.Nps, …)</c> case
/// references a <c>Scale.Nps</c> enum member that the implemented <c>Scale</c> enum is missing — even
/// though that enum's own XML doc already describes it ("Nps is the −100..+100 NPS scale"). The
/// implementer must add the <c>Nps</c> member to <c>Domain/ValueObjects/Scale.cs</c> for this file to
/// compile/pass; until then this is the valid "type doesn't exist yet" red.</para>
/// </summary>
public sealed class KpiNormalisationCalculatorTests
{
    [Fact]
    public void Normalise_returns_50_when_scale_1_5_raw_is_3()
    {
        KpiNormalisationCalculator.Normalise(Scale.Scale1_5, 3m).Should().Be(50m);
    }

    [Fact]
    public void Normalise_returns_100_when_scale_1_7_raw_is_max()
    {
        KpiNormalisationCalculator.Normalise(Scale.Scale1_7, 7m).Should().Be(100m);
    }

    [Fact]
    public void NormaliseCes_returns_0_when_effort_is_highest()
    {
        KpiNormalisationCalculator.NormaliseCes(7m).Should().Be(0m);
    }

    [Fact]
    public void NormaliseCes_returns_100_when_effort_is_lowest()
    {
        KpiNormalisationCalculator.NormaliseCes(1m).Should().Be(100m);
    }

    [Fact]
    public void NormaliseFcrBinary_returns_100_when_resolved()
    {
        KpiNormalisationCalculator.NormaliseFcrBinary(1m).Should().Be(100m);
    }

    [Fact]
    public void NormaliseFcrBinary_returns_0_when_not_resolved()
    {
        KpiNormalisationCalculator.NormaliseFcrBinary(0m).Should().Be(0m);
    }

    [Fact]
    public void Normalise_passes_through_when_scale_is_nps()
    {
        KpiNormalisationCalculator.Normalise(Scale.Nps, 42m).Should().Be(42m);
    }
}
