using Nabadat.SurveyBuilder.Domain.Entities;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>Question view (contracts/questions.md). Exposes <see cref="RowVersion"/> for the ETag.</summary>
public sealed record QuestionView(
    Guid Id,
    Guid SurveyId,
    Guid SectionId,
    Guid? SetId,
    QuestionType Type,
    QuestionSubType Subtype,
    string Text,
    string? Description,
    bool Required,
    bool Comments,
    string CommentLabel,
    int CommentMaxLength,
    bool Sentiment,
    string? KpiCode,
    string? Perspective,
    bool BoundJourneyOn,
    Guid? StageId,
    Guid? TouchpointId,
    QuestionTypePayload TypePayload,
    int Order,
    int RowVersion)
{
    public static QuestionView From(Question q) => new(
        q.Id, q.SurveyId, q.SectionId, q.SetId, q.Type, q.Subtype, q.Text, q.Description, q.Required,
        q.Comments, q.CommentLabel, q.CommentMaxLength, q.Sentiment, q.KpiCode, q.Perspective,
        q.BoundJourneyOn, q.StageId, q.TouchpointId, q.TypePayload, q.Order, q.RowVersion);
}
