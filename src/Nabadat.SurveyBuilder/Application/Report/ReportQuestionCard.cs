using Nabadat.SurveyBuilder.Domain.ValueObjects;

namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// One question's report card as an Application result (FR-13.3): the question identity + type, the
/// <see cref="ViewKind"/> its type maps to (chosen by <see cref="PerQuestionViewSelector"/>), and the
/// raw <see cref="Aggregate"/> (null when no in-window response answered it). The Api layer shapes
/// this into the wire <c>PerQuestionResult</c> + <c>PerQuestionView</c>.
/// </summary>
public sealed record ReportQuestionCard(
    Guid QuestionId,
    QuestionType Type,
    QuestionSubType Subtype,
    PerQuestionViewKind ViewKind,
    PerQuestionAggregate? Aggregate);
