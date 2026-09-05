using System.Collections.Concurrent;
using Nabadat.IntegrationHub.Domain.Interfaces;

namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// Default <see cref="ISurveyResolutionReader"/> binding until M-02 ships (research.md §4.3,
/// coordination-log.md C-01). <b>Always returns <c>null</c>, deterministically</b> — every scenario then
/// surfaces a clear "survey could not be resolved" internal error (<c>E-1500</c>) rather than silently
/// guessing a survey. Deterministic-null is the whole point: a stub that invented a survey id would let a
/// broken pipeline look healthy.
///
/// <para>Registered as a <b>singleton</b> so <see cref="Calls"/> survives the request scope and an
/// integration test can resolve it from the host's service provider and assert what the pipeline asked
/// for.</para>
/// </summary>
public sealed class NullSurveyResolutionReader : ISurveyResolutionReader
{
    private readonly ConcurrentQueue<RecordedSurveyResolution> _calls = new();

    /// <summary>Every resolution attempt, in order — the test-assertion surface for T113.</summary>
    public IReadOnlyCollection<RecordedSurveyResolution> Calls => _calls;

    /// <summary>Clears the recorded calls so a test can start from a known state.</summary>
    public void Reset() => _calls.Clear();

    public Task<Guid?> ResolveSurveyIdAsync(
        Guid serviceChannelId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct = default)
    {
        _calls.Enqueue(new RecordedSurveyResolution(serviceChannelId, parameters));
        return Task.FromResult<Guid?>(null);
    }
}
