using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T235 [US8] — maps a question's <see cref="QuestionType"/> (and, for Scale/InputField, its
/// <see cref="QuestionSubType"/>) to the report visual that renders its aggregate (FR-13.3). Pure
/// lookup; unit-tested by <c>PerQuestionViewSelectorTests</c> (T229). The selector picks the
/// <em>kind</em> of view — the aggregation itself is done by the ES aggregator (T239).
/// </summary>
public sealed class PerQuestionViewSelector
{
    /// <summary>Chooses the FR-13.3 view kind for a question.</summary>
    public PerQuestionViewKind Pick(QuestionType type, QuestionSubType subType = QuestionSubType.None) =>
        type switch
        {
            QuestionType.Kpi => PerQuestionViewKind.BarDistributionPlusGauge,
            QuestionType.SingleSelect => PerQuestionViewKind.DistributionDonut,
            QuestionType.YesNo => PerQuestionViewKind.DistributionDonut,
            QuestionType.MultiSelect => PerQuestionViewKind.BarWithCountsAndPct,
            QuestionType.Scale => PickScale(subType),
            QuestionType.InputField => PickInputField(subType),

            // FR-13.3 does not define a distinct report visual for Matrix or Ranking; a counts+%
            // bar is the sanest aggregate for both. Tracked as a spec gap (TODO-M01-024).
            QuestionType.Matrix => PerQuestionViewKind.BarWithCountsAndPct,
            QuestionType.Ranking => PerQuestionViewKind.BarWithCountsAndPct,

            _ => PerQuestionViewKind.BarWithCountsAndPct,
        };

    private static PerQuestionViewKind PickScale(QuestionSubType subType) => subType switch
    {
        QuestionSubType.Stars => PerQuestionViewKind.GaugePlusStars,
        QuestionSubType.Smileys => PerQuestionViewKind.GaugePlusFaces,
        // Labels (and Slider) have no side visual — the aggregate gauge alone.
        _ => PerQuestionViewKind.GaugeOnly,
    };

    private static PerQuestionViewKind PickInputField(QuestionSubType subType) => subType switch
    {
        QuestionSubType.Text => PerQuestionViewKind.VerbatimSample,
        QuestionSubType.Paragraph => PerQuestionViewKind.VerbatimSample,
        // Number / Date / Time / DateTime / Month → value-distribution line.
        _ => PerQuestionViewKind.ValueDistributionLine,
    };
}
