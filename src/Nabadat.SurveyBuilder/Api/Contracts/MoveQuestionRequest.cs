using Nabadat.SurveyBuilder.Application.Questions.Dtos;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/surveys/{id}/sections/{sectionId}/questions/{questionId}/move body (contracts/questions.md).</summary>
public sealed record MoveQuestionRequest(Guid TargetSectionId, Guid? TargetSetId, int TargetOrder)
{
    public MoveQuestionCommand ToCommand(Guid questionId, Guid actorId, Guid correlationId) =>
        new(questionId, TargetSectionId, TargetSetId, TargetOrder, actorId, correlationId);
}
