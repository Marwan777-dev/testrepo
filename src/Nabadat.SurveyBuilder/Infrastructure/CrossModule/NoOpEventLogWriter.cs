using Nabadat.SurveyBuilder.Domain.Interfaces;

namespace Nabadat.SurveyBuilder.Infrastructure.CrossModule;

/// <summary>
/// Placeholder <see cref="IEventLogWriter"/> until M-01 is wired to M-17's published event publisher
/// in the host (T020 — M-17's port is <c>IM17EventPublisher</c>; an adapter maps
/// <see cref="SurveyAuditEvent"/> onto it). This no-op drops the event so status changes are not
/// blocked in dev/E2E; production MUST replace it with the M-17 adapter (audit is mandatory,
/// constitution §5). Tracked as TODO-M01-011.
/// </summary>
public sealed class NoOpEventLogWriter : IEventLogWriter
{
    public Task WriteAsync(SurveyAuditEvent auditEvent, CancellationToken ct = default) => Task.CompletedTask;
}
