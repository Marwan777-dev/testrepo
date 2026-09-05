namespace Nabadat.SurveyBuilder.Application.Surveys.Dtos;

/// <summary>
/// Command for a destructive Return-to-Draft (BR-1.6) handled by
/// <c>DestructiveReturnToDraftService</c> (T072).
/// </summary>
public sealed record ReturnToDraftCommand(Guid SurveyId, Guid ActorId, Guid CorrelationId);
