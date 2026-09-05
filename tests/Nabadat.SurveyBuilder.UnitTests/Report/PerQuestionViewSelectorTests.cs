using FluentAssertions;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Domain.ValueObjects;
using Xunit;

namespace Nabadat.SurveyBuilder.UnitTests.Report;

/// <summary>
/// T229 [US8] — unit tests for <c>PerQuestionViewSelector</c> (FR-13.3). Maps a question's
/// <see cref="QuestionType"/> (and, for Scale/InputField, its <see cref="QuestionSubType"/>) to the
/// report visual that renders its aggregate. Pure lookup — the selector chooses the <em>kind</em> of
/// view; the actual aggregation is done elsewhere (T233/T239).
/// <para>
/// Contract pinned for the implementer (T235):
/// <list type="bullet">
///   <item><c>PerQuestionViewSelector</c> lives in <c>Application/Report/</c> and is pure.</item>
///   <item><c>PerQuestionViewKind Pick(QuestionType type, QuestionSubType subType = QuestionSubType.None)</c>.</item>
///   <item><c>PerQuestionViewKind</c> (enum in <c>Application/Report/</c>) members:
///   <c>BarDistributionPlusGauge</c> (KPI), <c>DistributionDonut</c> (SingleSelect / YesNo),
///   <c>BarWithCountsAndPct</c> (MultiSelect), <c>GaugeOnly</c> (Scale/Labels),
///   <c>GaugePlusStars</c> (Scale/Stars), <c>GaugePlusFaces</c> (Scale/Smileys — "a face for Faces"),
///   <c>VerbatimSample</c> (InputField/Text, InputField/Paragraph),
///   <c>ValueDistributionLine</c> (InputField Number / Date / Time).</item>
/// </list>
/// FR-13.3 verbatim: KPI → bar distribution + gauge; single-select &amp; Yes/No → donut + legend;
/// multi-select → bar with counts and %; scale → gauge + style visual (face for Faces, stars for Stars,
/// no side chart for Labels); text/paragraph → verbatim table; number/date/time → value-distribution line.
/// </para>
/// </summary>
public sealed class PerQuestionViewSelectorTests
{
    private readonly PerQuestionViewSelector _selector = new();

    [Fact]
    public void Pick_multi_select_returns_a_bar_with_counts_and_percentages()
    {
        // Required case: multi-select → bar chart with each option's count and % of respondents (FR-13.3/13.5).
        _selector.Pick(QuestionType.MultiSelect).Should().Be(PerQuestionViewKind.BarWithCountsAndPct);
    }

    [Fact]
    public void Pick_scale_with_labels_returns_gauge_only_with_no_side_chart()
    {
        // Required case: a Labels scale has no display-style visual — the aggregate gauge alone (FR-13.3).
        _selector.Pick(QuestionType.Scale, QuestionSubType.Labels).Should().Be(PerQuestionViewKind.GaugeOnly);
    }

    [Theory]
    // KPI → bar distribution + gauge.
    [InlineData(QuestionType.Kpi, QuestionSubType.None, PerQuestionViewKind.BarDistributionPlusGauge)]
    // Single-select and Yes/No → distribution donut with legend.
    [InlineData(QuestionType.SingleSelect, QuestionSubType.List, PerQuestionViewKind.DistributionDonut)]
    [InlineData(QuestionType.YesNo, QuestionSubType.None, PerQuestionViewKind.DistributionDonut)]
    // Scale display styles.
    [InlineData(QuestionType.Scale, QuestionSubType.Stars, PerQuestionViewKind.GaugePlusStars)]
    [InlineData(QuestionType.Scale, QuestionSubType.Smileys, PerQuestionViewKind.GaugePlusFaces)]
    // Text / paragraph → verbatim sample table.
    [InlineData(QuestionType.InputField, QuestionSubType.Text, PerQuestionViewKind.VerbatimSample)]
    [InlineData(QuestionType.InputField, QuestionSubType.Paragraph, PerQuestionViewKind.VerbatimSample)]
    // Number / date / time → value-distribution line.
    [InlineData(QuestionType.InputField, QuestionSubType.Number, PerQuestionViewKind.ValueDistributionLine)]
    [InlineData(QuestionType.InputField, QuestionSubType.Date, PerQuestionViewKind.ValueDistributionLine)]
    [InlineData(QuestionType.InputField, QuestionSubType.Time, PerQuestionViewKind.ValueDistributionLine)]
    public void Pick_maps_each_type_and_subtype_to_its_FR_13_3_view(
        QuestionType type, QuestionSubType subType, PerQuestionViewKind expected)
    {
        _selector.Pick(type, subType).Should().Be(expected);
    }
}
