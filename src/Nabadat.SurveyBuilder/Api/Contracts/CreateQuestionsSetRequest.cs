using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>POST /api/v1/surveys/{id}/sections/{sectionId}/sets body (contracts/sections-and-sets.md).</summary>
public sealed record CreateQuestionsSetRequest(
    Guid? Id,
    string? Title,
    string? Description,
    QuestionsSetSelectionMode SelectionMode = QuestionsSetSelectionMode.Random,
    int Count = 0,
    int Order = 0)
{
    public QuestionsSetWriteModel ToWriteModel(Guid sectionId) => new()
    {
        SectionId = sectionId,
        Title = Title ?? string.Empty,
        Description = Description,
        SelectionMode = SelectionMode,
        Count = Count,
        Order = Order,
    };
}
