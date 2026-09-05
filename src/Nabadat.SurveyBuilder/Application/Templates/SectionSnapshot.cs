namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// A section inside a <see cref="SurveySnapshot"/>, carrying its standalone
/// <see cref="Questions"/> and its rotating <see cref="Sets"/> (each with its own member
/// questions), mirroring the data-model.md §2.9 snapshot shape. Copied whole on save-as-template
/// (FR-7.4) and recreated with a fresh identity on instantiate (BR-7.1).
/// </summary>
public sealed record SectionSnapshot(
    Guid SectionId,
    string Name,
    IReadOnlyList<QuestionSnapshot> Questions)
{
    public string? Description { get; init; }

    public int Order { get; init; }

    public IReadOnlyList<QuestionsSetSnapshot> Sets { get; init; } = Array.Empty<QuestionsSetSnapshot>();
}
