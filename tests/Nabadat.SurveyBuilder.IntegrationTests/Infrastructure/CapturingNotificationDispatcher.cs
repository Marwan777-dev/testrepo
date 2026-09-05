using System.Collections.Concurrent;
using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.IntegrationTests.Infrastructure;

/// <summary>
/// Integration-test <see cref="INotificationDispatcher"/> that records every broadcast instead of
/// dispatching it (the production wiring is M-09's host adapter, which does not exist yet — the module
/// default is <c>NoOpNotificationDispatcher</c>, see TODO-M01-014). Registered as a singleton by
/// <see cref="SurveyBuilderApplicationFactory"/> so a test can assert the Q7 reviewer fan-out fired
/// (FR-15.2). Filter <see cref="Broadcasts"/> by the survey id in the deep link to isolate a test.
/// </summary>
public sealed class CapturingNotificationDispatcher : INotificationDispatcher
{
    private readonly ConcurrentQueue<CapturedBroadcast> _broadcasts = new();

    public IReadOnlyCollection<CapturedBroadcast> Broadcasts => _broadcasts;

    public Task BroadcastAsync(string scope, string permission, string deepLink, string template, CancellationToken ct)
    {
        _broadcasts.Enqueue(new CapturedBroadcast(scope, permission, deepLink, template));
        return Task.CompletedTask;
    }
}
