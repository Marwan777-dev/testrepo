using System.Text.Json.Serialization;
using Nabadat.SurveyBuilder.Application.Report;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>
/// One question's result card on the wire (FR-13.3, contracts/report-and-analytics.md — an entry in
/// <c>per_question</c>): the question identity + type/subtype, the chosen <see cref="View"/>, and the
/// number of responses that answered it (the top-right label).
/// </summary>
public sealed record PerQuestionResult(
    [property: JsonPropertyName("question_id")] Guid QuestionId,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("subtype")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Subtype,
    [property: JsonPropertyName("view")] PerQuestionView View,
    [property: JsonPropertyName("responses_count")] int ResponsesCount)
{
    /// <summary>Maps an Application-layer <see cref="ReportQuestionCard"/> to its wire shape.</summary>
    public static PerQuestionResult From(ReportQuestionCard card) => new(
        card.QuestionId,
        card.Type.ToString(),
        card.Subtype == QuestionSubType.None ? null : card.Subtype.ToString(),
        PerQuestionView.For(card.ViewKind, card.Aggregate),
        card.Aggregate?.ResponsesCount ?? 0);
}
