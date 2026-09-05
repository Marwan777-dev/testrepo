namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// Input to <see cref="Interfaces.IReportAggregator.GetVerbatimsAsync"/>: the "show more" verbatim
/// expansion for a single Text/Paragraph question (FR-13.7). Returns the newest
/// <see cref="Limit"/> in-window responses for the question.
/// </summary>
/// <param name="SurveyId">The survey being reported on.</param>
/// <param name="QuestionId">The Text/Paragraph question whose verbatims are requested.</param>
/// <param name="Limit">Maximum verbatims to return (default 20, max 100 — enforced at the controller).</param>
/// <param name="Period">The resolved reporting window.</param>
/// <param name="ActivePeriod">The survey's active-period length, or <c>null</c> when it never expires.</param>
/// <param name="Scope">The caller's data scope (Article 4.5).</param>
public sealed record VerbatimQuery(
    Guid SurveyId,
    Guid QuestionId,
    int Limit,
    ResolvedPeriod Period,
    TimeSpan? ActivePeriod,
    ReportScope Scope);
