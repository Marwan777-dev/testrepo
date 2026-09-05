namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// One bucket of a per-question distribution (FR-13.3): an option/answer <see cref="Label"/>, how
/// many responses picked it (<see cref="Count"/>), and — for multi-select — the percentage of
/// respondents who picked it (<see cref="PctOfRespondents"/>, which across all buckets may total
/// &gt; 100% per FR-13.5). <see cref="PctOfRespondents"/> is <c>null</c> for single-choice
/// distributions where the count already sums to the respondent base.
/// </summary>
public sealed record DistributionBucket(string Label, int Count, decimal? PctOfRespondents);
