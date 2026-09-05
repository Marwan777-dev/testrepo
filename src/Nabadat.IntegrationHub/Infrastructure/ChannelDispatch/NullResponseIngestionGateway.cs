using System.Collections.Concurrent;
using Nabadat.IntegrationHub.Domain.Interfaces;

namespace Nabadat.IntegrationHub.Infrastructure.ChannelDispatch;

/// <summary>
/// Default <see cref="IResponseIngestionGateway"/> binding until M-04 ships (research.md §4.4,
/// coordination-log.md C-02). A <b>no-op that records the call</b> — with no real M-04, the recording is
/// the only evidence an integration test can assert on.
///
/// <para><b>Consequence to be honest about:</b> while this stub is bound, an SCN-05 request returns
/// <c>202 ACCEPTED</c> and the response is <i>not durably stored anywhere</i>. That is acceptable for
/// dev/test and unacceptable for production; the real adapter must be registered before SCN-05 is exposed
/// to live callers.</para>
///
/// <para>Registered as a <b>singleton</b> so <see cref="Calls"/> outlives the request scope.</para>
/// </summary>
public sealed class NullResponseIngestionGateway : IResponseIngestionGateway
{
    private readonly ConcurrentQueue<RecordedResponseIngestion> _calls = new();

    /// <summary>Every forwarded response, in order — the test-assertion surface for T113/T115.</summary>
    public IReadOnlyCollection<RecordedResponseIngestion> Calls => _calls;

    /// <summary>Clears the recorded calls so a test can start from a known state.</summary>
    public void Reset() => _calls.Clear();

    public Task ForwardResponseAsync(
        Guid serviceChannelId,
        string transactionId,
        IReadOnlyDictionary<string, string> parameters,
        object surveyResponse,
        CancellationToken ct = default)
    {
        _calls.Enqueue(new RecordedResponseIngestion(serviceChannelId, transactionId, parameters, surveyResponse));
        return Task.CompletedTask;
    }
}
