namespace Nabadat.SurveyBuilder.Application.Questions.Dtos;

/// <summary>Command to delete a single question and clean up its routing + translations (T140).</summary>
public sealed record QuestionDeletionCommand(Guid QuestionId, Guid ActorId, Guid CorrelationId);
