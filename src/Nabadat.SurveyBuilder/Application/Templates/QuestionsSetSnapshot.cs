using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// A questions-set (rotating pool) inside a <see cref="SectionSnapshot"/> — copied whole on
/// save-as-template (FR-7.4) and recreated with a fresh identity on instantiate (BR-7.1).
/// </summary>
public sealed record QuestionsSetSnapshot(
    Guid SetId,
    string Title,
    QuestionsSetSelectionMode SelectionMode,
    int Count,
    int Order,
    IReadOnlyList<QuestionSnapshot> Questions)
{
    public string? Description { get; init; }
}
