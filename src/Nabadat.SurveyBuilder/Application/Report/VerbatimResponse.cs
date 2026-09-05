namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// A single verbatim (free-text) response surfaced in the report for a Text/Paragraph question
/// (FR-13.7). Carries the response identity, the <see cref="Channel"/> it arrived on and its
/// <see cref="SubmittedAt"/> time (both shown in the report table), and the answer <see cref="Text"/>.
/// Sampled newest-first by <see cref="VerbatimSampler"/>.
/// </summary>
public sealed record VerbatimResponse(Guid ResponseId, string Channel, DateTimeOffset SubmittedAt, string Text);
