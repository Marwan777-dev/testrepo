namespace Nabadat.SurveyBuilder.Application.Questions.Dtos;

/// <summary>
/// Command to move a question to a new placement (T142). <see cref="TargetSetId"/> null ⇒ standalone;
/// non-null ⇒ inside a Questions Set (which strips its routing per FR-9.5).
/// </summary>
public sealed record MoveQuestionCommand(
    Guid QuestionId,
    Guid TargetSectionId,
    Guid? TargetSetId,
    int TargetOrder,
    Guid ActorId,
    Guid CorrelationId);
