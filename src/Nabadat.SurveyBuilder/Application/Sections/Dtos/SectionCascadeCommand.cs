namespace Nabadat.SurveyBuilder.Application.Sections.Dtos;

/// <summary>
/// Command to delete a section and cascade its children (T138, FR-2.5). <see cref="Confirmed"/>
/// carries the <c>?confirm=true</c> client acknowledgement required when the section is non-empty.
/// </summary>
public sealed record SectionCascadeCommand(Guid SectionId, bool Confirmed, Guid ActorId, Guid CorrelationId);
