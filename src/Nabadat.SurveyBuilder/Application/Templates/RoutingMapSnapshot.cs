namespace Nabadat.SurveyBuilder.Application.Templates;

/// <summary>
/// A sparse per-answer routing override (F9) inside a <see cref="SurveySnapshot"/>. The
/// question ids reference the snapshot's original questions; on instantiate they are remapped to
/// the newly-generated question ids by <see cref="TemplateInstantiator"/> (a null
/// <see cref="TargetQuestionId"/> means "end of survey"). Copied on save-as-template (FR-7.4).
/// </summary>
public sealed record RoutingMapSnapshot(
    Guid SourceQuestionId,
    string AnswerKey,
    Guid? TargetQuestionId);
