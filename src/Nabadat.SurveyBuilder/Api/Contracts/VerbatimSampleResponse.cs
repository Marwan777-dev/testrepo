using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Report;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// A single verbatim response on the wire (FR-13.7, contracts/report-and-analytics.md — an entry in
/// a verbatim <c>sample</c>): the response id, the <c>channel</c> it arrived on, its
/// <c>submitted_at</c> time, and the answer <c>text</c>.
/// </summary>
public sealed record VerbatimSampleResponse(
    [property: JsonPropertyName("response_id")] Guid ResponseId,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("submitted_at")] DateTimeOffset SubmittedAt,
    [property: JsonPropertyName("text")] string Text)
{
    /// <summary>Maps an Application-layer <see cref="VerbatimResponse"/> to its wire shape.</summary>
    public static VerbatimSampleResponse From(VerbatimResponse response) =>
        new(response.ResponseId, response.Channel, response.SubmittedAt, response.Text);
}
