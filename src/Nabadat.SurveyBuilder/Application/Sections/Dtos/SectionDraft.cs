namespace Nabadat.SurveyBuilder.Application.Sections.Dtos;

/// <summary>
/// Validation input for a section create/update (T137). Carries the client-supplied fields the
/// <c>SectionValidator</c> checks — the name (1–200 chars) and the optional description.
/// </summary>
public sealed record SectionDraft
{
    public string? Name { get; init; }

    public string? Description { get; init; }
}
