using Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;
using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Api.Contracts;

/// <summary>PATCH /api/v1/surveys/{id}/sections/{sectionId}/sets/{setId} body (contracts/sections-and-sets.md).</summary>
public sealed record UpdateQuestionsSetRequest(
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
