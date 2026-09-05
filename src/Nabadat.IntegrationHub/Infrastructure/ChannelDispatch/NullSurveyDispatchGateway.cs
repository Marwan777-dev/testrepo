using System.Collections.Concurrent;
using Nabadat.IntegrationHub.Domain.Interfaces;

namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// Default <see cref="ISurveyDispatchGateway"/> binding until M-02 ships (research.md §4.3,
/// coordination-log.md C-01). A <b>no-op that records the call</b>: dispatch is fire-and-forget from
/// M-13's perspective, so dropping it changes no caller-visible behaviour, while the recording gives
/// integration tests the only available proof that the hand-off happened with the right payload.
///
/// <para>Registered as a <b>singleton</b> so <see cref="Calls"/> outlives the request scope.</para>
/// </summary>
public sealed class NullSurveyDispatchGateway : ISurveyDispatchGateway
{
    private readonly ConcurrentQueue<RecordedSurveyDispatch> _calls = new();

    /// <summary>Every dispatch hand-off, in order — the test-assertion surface for T113/T115.</summary>
    public IReadOnlyCollection<RecordedSurveyDispatch> Calls => _calls;

    /// <summary>Clears the recorded calls so a test can start from a known state.</summary>
    public void Reset() => _calls.Clear();

    public Task DispatchAsync(
        Guid surveyId,
        Guid serviceChannelId,
        IReadOnlyDictionary<string, string> parameters,
        Guid requestId,
        CancellationToken ct = default)
    {
        _calls.Enqueue(new RecordedSurveyDispatch(surveyId, serviceChannelId, parameters, requestId));
        return Task.CompletedTask;
    }
}
