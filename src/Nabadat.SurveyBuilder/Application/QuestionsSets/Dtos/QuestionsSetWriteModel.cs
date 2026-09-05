using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.QuestionsSets.Dtos;

/// <summary>Create/update input for a Questions Set (T139).</summary>
public sealed record QuestionsSetWriteModel
{
    public Guid SectionId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public QuestionsSetSelectionMode SelectionMode { get; init; } = QuestionsSetSelectionMode.Random;

    public int Count { get; init; }

    public int Order { get; init; }
}
