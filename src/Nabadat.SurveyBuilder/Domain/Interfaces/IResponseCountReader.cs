namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// M-01 read port over <b>M-04</b>'s per-survey response-count projection (research.md §7). The
/// low-response ordering algorithm (FR-10.4) reads how many times each question has been answered
/// to prioritise the least-answered ones; the counts live in Elasticsearch
/// (<c>tenant_{tenantId}_analytics</c> → one <c>question_response_counts</c> doc per question),
/// written by M-04's ingest. M-01 only reads — never writes — them, off the dispatch hot path.
/// <para>Implemented by <c>ResponseCountReader</c> (T145); a missing/absent projection resolves to
/// an empty map (every count 0), so ordering degrades gracefully to insertion order.</para>
/// </summary>
public interface IResponseCountReader
{
    /// <summary>Response counts keyed by question id for one survey. Absent questions ⇒ 0.</summary>
    Task<IReadOnlyDictionary<Guid, long>> GetResponseCountsAsync(Guid surveyId, CancellationToken ct = default);
}
