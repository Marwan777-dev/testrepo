using System.Text.Json.Serialization;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One responses-trend bucket on the wire (FR-14.4, contracts/report-and-analytics.md § GET
/// /analytics, <c>trend[]</c>): the bucket's start instant, its send and finished counts, and its
/// completion rate as a ratio in <c>[0,1]</c>.
/// </summary>
public sealed record TrendBucket(
    [property: JsonPropertyName("bucket_start")] DateTimeOffset BucketStart,
    [property: JsonPropertyName("sent")] long Sent,
    [property: JsonPropertyName("finished")] long Finished,
    [property: JsonPropertyName("completion_rate")] decimal CompletionRate);
