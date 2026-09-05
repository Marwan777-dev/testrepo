using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="INotificationDispatcher"/> until M-01 is wired to M-09's published
/// notification service in the host (T020 — M-09 does not exist under <c>src/</c> yet). This no-op
/// drops the broadcast so the submit-for-review flow is not blocked in dev/E2E; production MUST
/// replace it with the M-09 adapter (reviewer notification is required, FR-15.2). Tracked as
/// TODO-M01-014.
/// </summary>
public sealed class NoOpNotificationDispatcher : INotificationDispatcher
{
    public Task BroadcastAsync(string scope, string permission, string deepLink, string template, CancellationToken ct) =>
        Task.CompletedTask;
}
