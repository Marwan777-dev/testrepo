namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// The report visual chosen for a question's aggregate (FR-13.3). Chosen by
/// <see cref="PerQuestionViewSelector"/> from the question's type (and, for Scale/InputField, its
/// sub-type). The <c>kind</c> is serialised on the wire in snake_case
/// (contracts/report-and-analytics.md, e.g. <c>bar_with_counts_and_pct</c>).
/// </summary>
public enum PerQuestionViewKind
{
    /// <summary>KPI → bar distribution + a KPI gauge (response-count label top-right).</summary>
    BarDistributionPlusGauge,

    /// <summary>Single-select / Yes-No → distribution donut with a legend.</summary>
    DistributionDonut,

    /// <summary>Multi-select → bar chart with each option's count and % of respondents (may total &gt; 100%).</summary>
    BarWithCountsAndPct,

    /// <summary>Scale with the Labels display style → aggregate gauge only, no side chart.</summary>
    GaugeOnly,

    /// <summary>Scale with the Stars display style → aggregate gauge + filled-stars visual.</summary>
    GaugePlusStars,

    /// <summary>Scale with the Smileys display style ("a face for Faces") → aggregate gauge + face visual.</summary>
    GaugePlusFaces,

    /// <summary>Text / Paragraph → table of individual verbatim responses (channel + submission time).</summary>
    VerbatimSample,

    /// <summary>Number / Date / Time → value-distribution line (numeric additionally shows the average).</summary>
    ValueDistributionLine,
}
