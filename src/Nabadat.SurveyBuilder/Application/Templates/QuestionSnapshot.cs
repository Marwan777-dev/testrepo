using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// A single question inside a <see cref="SurveySnapshot"/> — a full copy of the source question's
/// authoring state (FR-7.4 copy-all). The journey binding is denormalised onto every question
/// (<see cref="JourneyId"/> from the survey, plus its own <see cref="StageId"/>/<see cref="TouchpointId"/>)
/// so a question's binding is self-contained in the snapshot and can be copied back verbatim on
/// instantiation (BR-7.1 snapshot-no-link). The positional members are the binding-critical ones
/// pinned by the US5 unit tests; the init-only members carry the remaining copied fields.
/// </summary>
public sealed record QuestionSnapshot(
    Guid QuestionId,
    string Text,
    QuestionType Type,
    Guid? JourneyId,
    Guid? StageId,
    Guid? TouchpointId)
{
    public Guid SectionId { get; init; }

    public Guid? SetId { get; init; }

    public QuestionSubType Subtype { get; init; } = QuestionSubType.None;

    public string? Description { get; init; }

    public bool Required { get; init; }

    public bool Comments { get; init; }

    public string CommentLabel { get; init; } = "Comments";

    public int CommentMaxLength { get; init; } = 200;

    public bool Sentiment { get; init; }

    public string? KpiCode { get; init; }

    public string? Perspective { get; init; }

    public bool BoundJourneyOn { get; init; } = true;

    public int Order { get; init; }

    public QuestionTypePayload? TypePayload { get; init; }
}
