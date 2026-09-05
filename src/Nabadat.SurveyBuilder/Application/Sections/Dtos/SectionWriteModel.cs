namespace Nabadat.SurveyBuilder.Application.Sections.Dtos;

/// <summary>Create/update input for a section (T137/T147). <see cref="Order"/> null ⇒ append to the end.</summary>
public sealed record SectionWriteModel
{
    public Guid SurveyId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? Order { get; init; }
}
