namespace Nabadat.SurveyBuilder.Application.Report;

/// <summary>
/// T237 [US8] — samples verbatim responses for a Text/Paragraph question's report card (FR-13.7):
/// newest-first, capped at the requested limit (the report shows the latest few by default and
/// expands to the last 100 via "show more"). Pure; unit-tested by <c>VerbatimSamplerTests</c> (T231).
/// </summary>
public sealed class VerbatimSampler
{
    /// <summary>
    /// Orders <paramref name="responses"/> by <see cref="VerbatimResponse.SubmittedAt"/> descending
    /// (newest first) and returns at most <paramref name="limit"/> of them.
    /// </summary>
    public IReadOnlyList<VerbatimResponse> Sample(IReadOnlyList<VerbatimResponse> responses, int limit) =>
        responses
            .OrderByDescending(r => r.SubmittedAt)
            .Take(limit)
            .ToList();
}
