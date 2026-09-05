namespace Nabadat.SurveyBuilder.Domain.Interfaces;

/// <summary>
/// Cross-module port M-01 consumes from <b>M-17 (Audit &amp; Event Log)</b> to append an event to the
/// tenant event log (constitution §5 — the audit log is owned by M-17; modules never write
/// <c>audit_log</c> directly). Every M-01 aggregate write emits one event via this port
/// (data-model.md §7).
/// <para><b>Declared here per T020;</b> the concrete implementation is supplied by M-17 and wired in
/// the host composition root. The unit-tested services take it as a mockable dependency.</para>
/// </summary>
public interface IEventLogWriter
{
    /// <summary>Appends <paramref name="auditEvent"/> to the tenant event log.</summary>
    Task WriteAsync(SurveyAuditEvent auditEvent, CancellationToken ct = default);
}
